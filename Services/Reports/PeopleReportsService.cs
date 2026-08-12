using System.Text.Json;
using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Reports;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Services.Reports;

/// <summary>
/// Reportes de la categoría Personas: desempeño de empleado, cumplimiento de
/// entregas, clientes nuevos vs recurrentes y cuentas por cobrar.
/// </summary>
public class PeopleReportsService
{
    private readonly AppDbContext _context;

    /// Acción con la que OrderService registra las transiciones de estado en
    /// audit_logs. Es la única fuente para medir tiempos de preparación.
    public const string AccionCambioEstado = "CAMBIO_ESTADO";

    public PeopleReportsService(AppDbContext context) => _context = context;

    // ── Desempeño de empleado ─────────────────────────────────────
    public async Task<StaffPerformanceReportDto> DesempenoAsync(ReportPeriod p)
    {
        var pedidos = await _context.Orders
            .Where(o => o.FechaCreacion >= p.DesdeUtc && o.FechaCreacion < p.HastaUtcExclusivo)
            .Select(o => new
            {
                o.Id,
                o.AtendidoPorUsuarioId,
                Empleado = o.AtendidoPor != null ? o.AtendidoPor.Nombre + " " + o.AtendidoPor.Apellido : null,
                o.Total,
                o.EstadoPedido
            })
            .ToListAsync();

        var atribuidos = pedidos.Where(o => o.AtendidoPorUsuarioId.HasValue).ToList();

        var reporte = new StaffPerformanceReportDto
        {
            TotalPedidos        = pedidos.Count,
            PedidosSinAtribuir  = pedidos.Count - atribuidos.Count,
            CoberturaAtribucion = pedidos.Count == 0
                ? 0m
                : Math.Round((decimal)atribuidos.Count / pedidos.Count * 100, 1)
        };

        var tiempos = await TiemposPreparacionAsync(p);
        reporte.PedidosConTiempoMedido = tiempos.Count;

        // Entregas del periodo, para la parte de repartidor.
        var entregas = await _context.Deliveries
            .Where(d => d.FechaProgramada >= p.Desde && d.FechaProgramada <= p.Hasta && d.RepartidorId != null)
            .Select(d => new
            {
                RepartidorId = d.RepartidorId!.Value,
                Repartidor   = d.Repartidor!.Nombre + " " + d.Repartidor.Apellido,
                d.FechaProgramada,
                d.HoraProgramada,
                d.FechaReal
            })
            .ToListAsync();

        var porRepartidor = entregas
            .GroupBy(d => new { d.RepartidorId, d.Repartidor })
            .ToDictionary(
                g => g.Key.RepartidorId,
                g => new
                {
                    g.Key.Repartidor,
                    Asignadas = g.Count(),
                    ATiempo   = g.Count(d => d.FechaReal.HasValue &&
                                             d.FechaReal.Value <= LimiteEntregaUtc(d.FechaProgramada, d.HoraProgramada))
                });

        var empleados = atribuidos
            .GroupBy(o => new { Id = o.AtendidoPorUsuarioId!.Value, o.Empleado })
            .Select(g =>
            {
                var vigentes  = g.Where(o => o.EstadoPedido != "CANCELADO").ToList();
                var monto     = vigentes.Sum(o => o.Total);
                var noCompl   = g.Count(o => o.EstadoPedido == "NO_COMPLETADO");
                var propios   = g.Select(o => o.Id).Where(tiempos.ContainsKey).ToList();

                return new StaffMemberDto
                {
                    UsuarioId            = g.Key.Id,
                    Nombre               = (g.Key.Empleado ?? "Usuario dado de baja").Trim(),
                    PedidosAtendidos     = g.Count(),
                    MontoVendido         = monto,
                    TicketPromedio       = vigentes.Count > 0 ? Math.Round(monto / vigentes.Count, 2) : 0m,
                    PedidosNoCompletados = noCompl,
                    PctNoCompletados     = Math.Round((decimal)noCompl / g.Count() * 100, 1),
                    MinutosPreparacion   = propios.Count > 0
                        ? Math.Round(propios.Average(id => tiempos[id]), 1)
                        : null
                };
            })
            .ToList();

        // Un repartidor puede no haber capturado ningún pedido: aun así debe salir
        // en el reporte con sus métricas de entrega.
        foreach (var (repartidorId, datos) in porRepartidor)
        {
            var fila = empleados.FirstOrDefault(e => e.UsuarioId == repartidorId);
            if (fila is null)
            {
                fila = new StaffMemberDto { UsuarioId = repartidorId, Nombre = datos.Repartidor.Trim() };
                empleados.Add(fila);
            }

            fila.EntregasAsignadas  = datos.Asignadas;
            fila.EntregasATiempo    = datos.ATiempo;
            fila.PctEntregasATiempo = ReportAggregation.Pct(datos.ATiempo, datos.Asignadas);
        }

        reporte.Empleados = empleados
            .OrderByDescending(e => e.MontoVendido)
            .ThenByDescending(e => e.EntregasAsignadas)
            .ToList();

        return reporte;
    }

    /// <summary>
    /// Minutos de preparación por pedido, leídos de audit_logs: del momento en que
    /// el pedido entra a EN_PREPARACION al momento en que sale hacia EN_RUTA o
    /// ENTREGADO. Solo existen registros desde que se instrumentó el cambio de
    /// estado, así que los pedidos viejos simplemente no aparecen en el diccionario.
    /// </summary>
    private async Task<Dictionary<Guid, double>> TiemposPreparacionAsync(ReportPeriod p)
    {
        var logs = await _context.AuditLogs
            .Where(l => l.Accion == AccionCambioEstado && l.Entidad == "Order" && l.EntidadId != null &&
                        l.FechaHora >= p.DesdeUtc && l.FechaHora < p.HastaUtcExclusivo.AddDays(7))
            .OrderBy(l => l.FechaHora)
            .Select(l => new { l.EntidadId, l.Detalles, l.FechaHora })
            .ToListAsync();

        var inicio = new Dictionary<Guid, DateTime>();
        var minutos = new Dictionary<Guid, double>();

        foreach (var log in logs)
        {
            if (!Guid.TryParse(log.EntidadId, out var orderId)) continue;

            var nuevo = LeerEstadoNuevo(log.Detalles);
            if (nuevo is null) continue;

            if (nuevo == "EN_PREPARACION")
            {
                inicio[orderId] = log.FechaHora;
            }
            else if (nuevo is "EN_RUTA" or "ENTREGADO" && inicio.TryGetValue(orderId, out var desde))
            {
                minutos[orderId] = (log.FechaHora - desde).TotalMinutes;
                inicio.Remove(orderId);
            }
        }

        return minutos;
    }

    private static string? LeerEstadoNuevo(string? detallesJson)
    {
        if (string.IsNullOrWhiteSpace(detallesJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(detallesJson);
            return doc.RootElement.TryGetProperty("nuevo", out var nuevo) ? nuevo.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Instante límite de una entrega, en UTC. Sin hora programada el compromiso
    /// es "ese día", así que el límite es el final del día local.
    /// </summary>
    private static DateTime LimiteEntregaUtc(DateOnly fecha, TimeOnly? hora)
    {
        var local = fecha.ToDateTime(hora ?? new TimeOnly(23, 59, 59));
        return DateTime.SpecifyKind(local.AddHours(-ReportPeriod.OffsetHorasLocal), DateTimeKind.Utc);
    }

    // ── Cumplimiento de entregas ──────────────────────────────────
    public async Task<DeliveryFulfillmentReportDto> EntregasAsync(ReportPeriod p, ReportGranularity granularidad)
    {
        var entregas = await _context.Deliveries
            .Where(d => d.FechaProgramada >= p.Desde && d.FechaProgramada <= p.Hasta)
            .Select(d => new
            {
                d.OrderId,
                Cliente    = d.Order.Customer.Nombre,
                d.FechaProgramada,
                d.HoraProgramada,
                d.FechaReal,
                d.EstadoEntrega,
                EstadoPedido = d.Order.EstadoPedido,
                Repartidor   = d.Repartidor != null ? d.Repartidor.Nombre + " " + d.Repartidor.Apellido : null
            })
            .ToListAsync();

        var evaluadas = entregas
            .Select(d => new
            {
                d.OrderId, d.Cliente, d.FechaProgramada, d.HoraProgramada, d.FechaReal,
                d.EstadoEntrega, d.EstadoPedido, d.Repartidor,
                RetrasoMin = d.FechaReal.HasValue
                    ? (d.FechaReal.Value - LimiteEntregaUtc(d.FechaProgramada, d.HoraProgramada)).TotalMinutes
                    : (double?)null
            })
            .ToList();

        var conFecha  = evaluadas.Where(d => d.RetrasoMin.HasValue).ToList();
        var aTiempo   = conFecha.Count(d => d.RetrasoMin!.Value <= 0);
        var retrasadas= conFecha.Where(d => d.RetrasoMin!.Value > 0).ToList();
        var noCompl   = evaluadas.Count(d => d.EstadoPedido == "NO_COMPLETADO");

        return new DeliveryFulfillmentReportDto
        {
            TotalEntregas        = evaluadas.Count,
            Entregadas           = conFecha.Count,
            ATiempo              = aTiempo,
            ConRetraso           = retrasadas.Count,
            SinFechaReal         = evaluadas.Count - conFecha.Count,
            PctATiempo           = ReportAggregation.Pct(aTiempo, conFecha.Count),
            RetrasoPromedioMin   = retrasadas.Count > 0
                ? Math.Round(retrasadas.Average(d => d.RetrasoMin!.Value), 1)
                : null,
            PedidosNoCompletados = noCompl,
            PctNoCompletados     = ReportAggregation.Pct(noCompl, evaluadas.Count),

            Serie = ReportAggregation.Serie(p, granularidad,
                evaluadas.Select(d => (d.FechaProgramada, 1, (decimal)(d.RetrasoMin > 0 ? 1 : 0)))),

            PorEstado = ReportAggregation.Corte(
                evaluadas.Select(d => (string.IsNullOrWhiteSpace(d.EstadoEntrega) ? "SIN ESTADO" : d.EstadoEntrega, 0m))),

            MasRetrasadas = retrasadas
                .OrderByDescending(d => d.RetrasoMin)
                .Take(25)
                .Select(d => new DeliveryLateDto
                {
                    OrderId         = d.OrderId,
                    Cliente         = d.Cliente,
                    FechaProgramada = d.FechaProgramada,
                    HoraProgramada  = d.HoraProgramada,
                    FechaReal       = d.FechaReal,
                    RetrasoMinutos  = Math.Round(d.RetrasoMin!.Value, 1),
                    Repartidor      = d.Repartidor?.Trim()
                })
                .ToList()
        };
    }

    // ── Clientes: nuevos vs recurrentes ───────────────────────────
    public async Task<CustomerRetentionReportDto> ClientesAsync(ReportPeriod p, ReportGranularity granularidad)
    {
        var pedidos = (await _context.Orders
            .Where(o => o.FechaCreacion >= p.DesdeUtc && o.FechaCreacion < p.HastaUtcExclusivo &&
                        o.EstadoPedido != "CANCELADO")
            .Select(o => new { o.CustomerId, Cliente = o.Customer.Nombre, o.FechaCreacion, o.Total })
            .ToListAsync())
            .Select(o => new { o.CustomerId, o.Cliente, Fecha = ReportPeriod.ALocal(o.FechaCreacion), o.Total })
            .ToList();

        if (pedidos.Count == 0)
        {
            return new CustomerRetentionReportDto
            {
                SerieNuevos      = ReportAggregation.Serie(p, granularidad, []),
                SerieRecurrentes = ReportAggregation.Serie(p, granularidad, []),
                PorSegmento      = await SegmentosAsync()
            };
        }

        var clientes = pedidos.Select(o => o.CustomerId).Distinct().ToList();

        // Un cliente es "nuevo" si su primera compra de la historia cae dentro del
        // periodo. Si ya compraba antes, es recurrente aunque en este periodo solo
        // haya comprado una vez.
        var primeraCompra = (await _context.Orders
            .Where(o => clientes.Contains(o.CustomerId) && o.EstadoPedido != "CANCELADO")
            .GroupBy(o => o.CustomerId)
            .Select(g => new { CustomerId = g.Key, Primera = g.Min(o => o.FechaCreacion) })
            .ToListAsync())
            .ToDictionary(x => x.CustomerId, x => x.Primera);

        bool EsNuevo(Guid customerId) =>
            primeraCompra.TryGetValue(customerId, out var primera) && primera >= p.DesdeUtc;

        var nuevos      = clientes.Where(EsNuevo).ToHashSet();
        var recurrentes = clientes.Where(c => !nuevos.Contains(c)).ToHashSet();

        var pedidosNuevos      = pedidos.Where(o => nuevos.Contains(o.CustomerId)).ToList();
        var pedidosRecurrentes = pedidos.Where(o => recurrentes.Contains(o.CustomerId)).ToList();

        var ventasNuevos      = pedidosNuevos.Sum(o => o.Total);
        var ventasRecurrentes = pedidosRecurrentes.Sum(o => o.Total);

        return new CustomerRetentionReportDto
        {
            ClientesActivos     = clientes.Count,
            ClientesNuevos      = nuevos.Count,
            ClientesRecurrentes = recurrentes.Count,
            TasaRecompra        = ReportAggregation.Pct(recurrentes.Count, clientes.Count),
            VentasNuevos        = ventasNuevos,
            VentasRecurrentes   = ventasRecurrentes,
            TicketNuevos        = pedidosNuevos.Count > 0
                ? Math.Round(ventasNuevos / pedidosNuevos.Count, 2) : null,
            TicketRecurrentes   = pedidosRecurrentes.Count > 0
                ? Math.Round(ventasRecurrentes / pedidosRecurrentes.Count, 2) : null,

            SerieNuevos      = ReportAggregation.Serie(p, granularidad,
                pedidosNuevos.Select(o => (o.Fecha, 1, o.Total))),
            SerieRecurrentes = ReportAggregation.Serie(p, granularidad,
                pedidosRecurrentes.Select(o => (o.Fecha, 1, o.Total))),

            PorSegmento = await SegmentosAsync(),

            TopClientes = pedidos
                .GroupBy(o => new { o.CustomerId, o.Cliente })
                .Select(g => new TopCustomerDto
                {
                    CustomerId   = g.Key.CustomerId,
                    Nombre       = g.Key.Cliente,
                    Pedidos      = g.Count(),
                    TotalGastado = g.Sum(o => o.Total)
                })
                .OrderByDescending(c => c.TotalGastado)
                .Take(10)
                .ToList()
        };
    }

    /// Segmentación RFM ya calculada por CustomerSegmentationService. Devuelve
    /// lista vacía si el modelo todavía no se ha ejecutado nunca.
    private async Task<List<BreakdownDto>> SegmentosAsync()
    {
        var segmentos = await _context.CustomerSegments
            .Select(s => new { s.Grupo, s.MontoTotal })
            .ToListAsync();

        return ReportAggregation.Corte(segmentos.Select(s =>
            (string.IsNullOrWhiteSpace(s.Grupo) ? "SIN CLASIFICAR" : s.Grupo.Trim().ToUpperInvariant(),
             s.MontoTotal)));
    }

    // ── Cuentas por cobrar ────────────────────────────────────────
    /// <summary>
    /// El saldo por cobrar es un SALDO, no un flujo: siempre se reporta a hoy, sin
    /// importar el rango. El rango solo acota cuánto se cobró en el periodo.
    /// </summary>
    public async Task<ReceivablesReportDto> CuentasPorCobrarAsync(ReportPeriod p)
    {
        var pendientes = await _context.Orders
            .Where(o => o.SaldoPendiente > 0 && o.EstadoPedido != "CANCELADO")
            .Select(o => new
            {
                o.Id,
                Cliente  = o.Customer.Nombre,
                Telefono = o.Customer.Telefono,
                o.FechaCreacion,
                o.FechaEntrega,
                o.EstadoPedido,
                o.Total,
                o.SaldoPendiente
            })
            .ToListAsync();

        var ahora = DateTime.UtcNow;

        var filas = pendientes
            .Select(o => new ReceivableDto
            {
                OrderId        = o.Id,
                Cliente        = o.Cliente,
                Telefono       = o.Telefono,
                FechaCreacion  = o.FechaCreacion,
                FechaEntrega   = o.FechaEntrega,
                EstadoPedido   = o.EstadoPedido,
                Total          = o.Total,
                Pagado         = o.Total - o.SaldoPendiente,
                SaldoPendiente = o.SaldoPendiente,
                DiasAntiguedad = Math.Max(0, (int)(ahora - o.FechaCreacion).TotalDays)
            })
            .OrderByDescending(o => o.SaldoPendiente)
            .ToList();

        var cobrado = await _context.Payments
            .Where(pg => pg.FechaPago >= p.DesdeUtc && pg.FechaPago < p.HastaUtcExclusivo &&
                         pg.Estado != "CANCELADO")
            .SumAsync(pg => (decimal?)pg.Monto) ?? 0m;

        return new ReceivablesReportDto
        {
            TotalPorCobrar   = filas.Sum(f => f.SaldoPendiente),
            PedidosConSaldo  = filas.Count,
            CobradoEnPeriodo = cobrado,
            PorAntiguedad    = ReportAggregation.Corte(
                filas.Select(f => (BucketAntiguedad(f.DiasAntiguedad), f.SaldoPendiente))),
            Pedidos          = filas
        };
    }

    private static string BucketAntiguedad(int dias) => dias switch
    {
        <= 15 => "0-15 DÍAS",
        <= 30 => "16-30 DÍAS",
        <= 60 => "31-60 DÍAS",
        _     => "MÁS DE 60 DÍAS"
    };
}
