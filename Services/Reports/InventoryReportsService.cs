using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Reports;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Services.Reports;

/// <summary>
/// Reportes de la categoría Inventario: movimientos de insumos (kardex), merma y
/// stock sin rotación.
///
/// Toda la aritmética de saldos respeta la convención de inventory_movements:
/// ENTRADA suma, SALIDA resta y AJUSTE FIJA el stock en un valor absoluto (ver
/// InventoryService.RegistrarMovimientoAsync). Sumar la columna Cantidad de un
/// AJUSTE como si fuera un delta es el error clásico aquí y produce saldos que no
/// cuadran con lo que hay en la bodega.
/// </summary>
public class InventoryReportsService
{
    private readonly AppDbContext _context;

    public InventoryReportsService(AppDbContext context) => _context = context;

    /// Una fila del historial de movimientos, ya aplanada.
    private sealed record MovRow(
        Guid Id, DateTime FechaHora, string Tipo, int Cantidad, string? Motivo, string Usuario);

    // ── Movimientos de insumos ────────────────────────────────────
    public async Task<InventoryMovementsReportDto> MovimientosAsync(
        ReportPeriod p, ReportGranularity granularidad,
        Guid? itemId, string? tipo, Guid? usuarioId)
    {
        var query = _context.InventoryMovements
            .Where(m => m.FechaHora >= p.DesdeUtc && m.FechaHora < p.HastaUtcExclusivo);

        if (itemId.HasValue)    query = query.Where(m => m.InventoryItemId == itemId.Value);
        if (usuarioId.HasValue) query = query.Where(m => m.UsuarioId == usuarioId.Value);
        if (!string.IsNullOrWhiteSpace(tipo))
        {
            var t = tipo.Trim().ToUpperInvariant();
            query = query.Where(m => m.TipoMovimiento == t);
        }

        var movimientos = (await query
            .Select(m => new
            {
                m.Id,
                m.InventoryItemId,
                Nombre       = m.InventoryItem.Nombre,
                UnidadMedida = m.InventoryItem.UnidadMedida,
                PrecioCosto  = m.InventoryItem.PrecioCosto,
                StockActual  = m.InventoryItem.StockActual,
                m.TipoMovimiento,
                m.Cantidad,
                m.FechaHora,
                Usuario = m.Usuario.Nombre + " " + m.Usuario.Apellido
            })
            .ToListAsync())
            .Select(m => new { m.Id, m.InventoryItemId, m.Nombre, m.UnidadMedida, m.PrecioCosto,
                               m.StockActual, m.TipoMovimiento, m.Cantidad, m.Usuario,
                               Fecha = ReportPeriod.ALocal(m.FechaHora) })
            .ToList();

        var entradas = movimientos.Where(m => m.TipoMovimiento == "ENTRADA").ToList();
        var salidas  = movimientos.Where(m => m.TipoMovimiento == "SALIDA").ToList();

        var reporte = new InventoryMovementsReportDto
        {
            TotalMovimientos = movimientos.Count,
            TotalEntradas    = entradas.Sum(m => m.Cantidad),
            TotalSalidas     = salidas.Sum(m => m.Cantidad),
            // Los AJUSTE se cuentan, no se suman: su Cantidad es un stock final,
            // no unidades que entraron o salieron.
            TotalAjustes     = movimientos.Count(m => m.TipoMovimiento == "AJUSTE"),
            ValorEntradas    = Math.Round(entradas.Sum(m => m.Cantidad * m.PrecioCosto), 2),
            ValorSalidas     = Math.Round(salidas .Sum(m => m.Cantidad * m.PrecioCosto), 2),

            SerieEntradas = ReportAggregation.Serie(p, granularidad,
                entradas.Select(m => (m.Fecha, m.Cantidad, m.Cantidad * m.PrecioCosto))),
            SerieSalidas  = ReportAggregation.Serie(p, granularidad,
                salidas.Select(m => (m.Fecha, m.Cantidad, m.Cantidad * m.PrecioCosto))),

            TopConsumo = movimientos
                .GroupBy(m => new { m.InventoryItemId, m.Nombre, m.UnidadMedida, m.PrecioCosto, m.StockActual })
                .Select(g => new SupplyConsumptionDto
                {
                    InventoryItemId = g.Key.InventoryItemId,
                    Nombre          = g.Key.Nombre,
                    UnidadMedida    = g.Key.UnidadMedida,
                    PrecioCosto     = g.Key.PrecioCosto,
                    StockActual     = g.Key.StockActual,
                    Consumido       = g.Where(x => x.TipoMovimiento == "SALIDA").Sum(x => x.Cantidad),
                    Recibido        = g.Where(x => x.TipoMovimiento == "ENTRADA").Sum(x => x.Cantidad),
                    ValorConsumido  = Math.Round(
                        g.Where(x => x.TipoMovimiento == "SALIDA").Sum(x => x.Cantidad) * g.Key.PrecioCosto, 2)
                })
                .OrderByDescending(x => x.Consumido)
                .ToList(),

            PorUsuario = ReportAggregation.Corte(movimientos.Select(m =>
                (string.IsNullOrWhiteSpace(m.Usuario) ? "SIN USUARIO" : m.Usuario.Trim(),
                 m.TipoMovimiento == "AJUSTE" ? 0m : m.Cantidad * m.PrecioCosto)))
        };

        if (itemId.HasValue)
            reporte.Kardex = await KardexAsync(p, itemId.Value, tipo, usuarioId);

        return reporte;
    }

    /// <summary>
    /// Kardex con saldo corrido de un insumo. El saldo se calcula sobre TODOS los
    /// movimientos del periodo aunque haya filtros activos: un saldo calculado
    /// sobre una lista filtrada no sería el saldo de la bodega. Los filtros solo
    /// deciden qué líneas se muestran.
    /// </summary>
    private async Task<KardexDto?> KardexAsync(ReportPeriod p, Guid itemId, string? tipo, Guid? usuarioId)
    {
        var item = await _context.InventoryItems
            .Where(i => i.Id == itemId)
            .Select(i => new { i.Id, i.Nombre, i.UnidadMedida, i.PrecioCosto, i.StockActual })
            .FirstOrDefaultAsync();
        if (item is null) return null;

        // Historial completo del insumo: hace falta para anclar el saldo inicial.
        var historial = await HistorialAsync(itemId);

        var saldo = SaldoAlInicio(historial, p.DesdeUtc, item.StockActual);

        var kardex = new KardexDto
        {
            InventoryItemId = item.Id,
            Nombre          = item.Nombre,
            UnidadMedida    = item.UnidadMedida,
            PrecioCosto     = item.PrecioCosto,
            SaldoInicial    = saldo
        };

        var tipoFiltro = string.IsNullOrWhiteSpace(tipo) ? null : tipo.Trim().ToUpperInvariant();
        var usuariosPorMovimiento = usuarioId.HasValue
            ? (await _context.InventoryMovements
                .Where(m => m.InventoryItemId == itemId && m.UsuarioId == usuarioId.Value)
                .Select(m => m.Id)
                .ToListAsync()).ToHashSet()
            : null;

        foreach (var m in historial.Where(m => m.FechaHora >= p.DesdeUtc && m.FechaHora < p.HastaUtcExclusivo))
        {
            var antes = saldo;
            saldo = Aplicar(saldo, m);

            var visible = (tipoFiltro is null || m.Tipo == tipoFiltro) &&
                          (usuariosPorMovimiento is null || usuariosPorMovimiento.Contains(m.Id));
            if (!visible) continue;

            kardex.Lineas.Add(new KardexLineDto
            {
                MovimientoId = m.Id,
                FechaHora    = m.FechaHora,
                Tipo         = m.Tipo,
                Cantidad     = m.Cantidad,
                Delta        = Delta(antes, m),
                Saldo        = saldo,
                Motivo       = m.Motivo,
                Usuario      = m.Usuario.Trim()
            });
        }

        kardex.SaldoFinal = saldo ?? item.StockActual;
        return kardex;
    }

    private async Task<List<MovRow>> HistorialAsync(Guid itemId) =>
        await _context.InventoryMovements
            .Where(m => m.InventoryItemId == itemId)
            .OrderBy(m => m.FechaHora).ThenBy(m => m.Id)
            .Select(m => new MovRow(m.Id, m.FechaHora, m.TipoMovimiento, m.Cantidad, m.Motivo,
                                    m.Usuario.Nombre + " " + m.Usuario.Apellido))
            .ToListAsync();

    /// <summary>
    /// Saldo del insumo justo antes de `desdeUtc`.
    ///
    /// Un AJUSTE es un ancla: deja el stock en un valor conocido sin importar lo
    /// que hubiera antes. Se busca el ancla más reciente anterior al periodo y se
    /// avanza desde ahí. Si no hay ninguno pero tampoco existe ningún AJUSTE en
    /// toda la historia, se puede retroceder desde el stock actual restando los
    /// deltas posteriores. Si el primer AJUSTE del insumo cae dentro o después del
    /// periodo, el saldo previo es genuinamente desconocido y se devuelve null.
    /// </summary>
    private static int? SaldoAlInicio(List<MovRow> historial, DateTime desdeUtc, int stockActual)
    {
        var previos = historial.Where(m => m.FechaHora < desdeUtc).ToList();
        var indiceAncla = previos.FindLastIndex(m => m.Tipo == "AJUSTE");

        if (indiceAncla >= 0)
        {
            var saldo = previos[indiceAncla].Cantidad;
            for (var i = indiceAncla + 1; i < previos.Count; i++)
                saldo += DeltaSimple(previos[i]);
            return saldo;
        }

        if (historial.All(m => m.Tipo != "AJUSTE"))
            return stockActual - historial.Where(m => m.FechaHora >= desdeUtc).Sum(DeltaSimple);

        return null;
    }

    private static int DeltaSimple(MovRow m) => m.Tipo switch
    {
        "ENTRADA" => m.Cantidad,
        "SALIDA"  => -m.Cantidad,
        _         => 0 // AJUSTE no tiene delta propio; se maneja como ancla
    };

    private static int? Aplicar(int? saldo, MovRow m) => m.Tipo switch
    {
        "AJUSTE"  => m.Cantidad,                       // fija el stock
        "ENTRADA" => saldo.HasValue ? saldo + m.Cantidad : null,
        "SALIDA"  => saldo.HasValue ? saldo - m.Cantidad : null,
        _         => saldo
    };

    private static int? Delta(int? saldoAntes, MovRow m) => m.Tipo switch
    {
        "AJUSTE"  => saldoAntes.HasValue ? m.Cantidad - saldoAntes.Value : null,
        "ENTRADA" => m.Cantidad,
        "SALIDA"  => -m.Cantidad,
        _         => 0
    };

    // ── Merma y caducidad ─────────────────────────────────────────
    /// <summary>
    /// Merma = pérdida provocada por un AJUSTE que bajó el stock. Se reconstruye
    /// el saldo previo de cada insumo para saber cuántas unidades desaparecieron;
    /// la columna Cantidad del AJUSTE por sí sola no lo dice.
    /// </summary>
    public async Task<WasteReportDto> MermaAsync(ReportPeriod p, ReportGranularity granularidad)
    {
        var itemsConAjuste = await _context.InventoryMovements
            .Where(m => m.TipoMovimiento == "AJUSTE" &&
                        m.FechaHora >= p.DesdeUtc && m.FechaHora < p.HastaUtcExclusivo)
            .Select(m => m.InventoryItemId)
            .Distinct()
            .ToListAsync();

        var reporte = new WasteReportDto
        {
            Serie = ReportAggregation.Serie(p, granularidad, [])
        };

        if (itemsConAjuste.Count == 0)
        {
            reporte.PorcentajeSobreConsumo = null;
            return reporte;
        }

        var items = await _context.InventoryItems
            .Where(i => itemsConAjuste.Contains(i.Id))
            .Select(i => new { i.Id, i.Nombre, i.UnidadMedida, i.PrecioCosto, i.StockActual })
            .ToListAsync();

        var perdidas   = new List<(DateOnly Fecha, int Unidades, decimal Valor, string Motivo, Guid ItemId)>();
        var porInsumo  = new List<WasteItemDto>();

        foreach (var item in items)
        {
            var historial = await HistorialAsync(item.Id);
            var saldo     = SaldoAlInicio(historial, p.DesdeUtc, item.StockActual);

            var unidadesItem = 0;
            var eventosItem  = 0;

            foreach (var m in historial.Where(m => m.FechaHora >= p.DesdeUtc && m.FechaHora < p.HastaUtcExclusivo))
            {
                var antes = saldo;
                saldo = Aplicar(saldo, m);

                if (m.Tipo != "AJUSTE") continue;

                var delta = Delta(antes, m);
                // Sin saldo previo conocido no se puede afirmar que hubo pérdida:
                // se omite en vez de asumir que todo el ajuste fue merma.
                if (delta is null or >= 0) continue;

                var unidades = -delta.Value;
                var valor    = Math.Round(unidades * item.PrecioCosto, 2);

                unidadesItem += unidades;
                eventosItem++;
                perdidas.Add((ReportPeriod.ALocal(m.FechaHora), unidades, valor,
                              string.IsNullOrWhiteSpace(m.Motivo) ? "SIN MOTIVO" : m.Motivo.Trim(), item.Id));
            }

            if (unidadesItem > 0)
            {
                porInsumo.Add(new WasteItemDto
                {
                    InventoryItemId = item.Id,
                    Nombre          = item.Nombre,
                    UnidadMedida    = item.UnidadMedida,
                    Unidades        = unidadesItem,
                    Valor           = Math.Round(unidadesItem * item.PrecioCosto, 2),
                    Eventos         = eventosItem
                });
            }
        }

        // Referencia contra la que se mide: valor de todo lo que salió a producción.
        var valorConsumo = await _context.InventoryMovements
            .Where(m => m.TipoMovimiento == "SALIDA" &&
                        m.FechaHora >= p.DesdeUtc && m.FechaHora < p.HastaUtcExclusivo)
            .SumAsync(m => (decimal?)(m.Cantidad * m.InventoryItem.PrecioCosto)) ?? 0m;

        var valorPerdido = perdidas.Sum(x => x.Valor);

        reporte.UnidadesPerdidas       = perdidas.Sum(x => x.Unidades);
        reporte.ValorPerdido           = Math.Round(valorPerdido, 2);
        reporte.PorcentajeSobreConsumo = ReportAggregation.Pct(valorPerdido, valorConsumo + valorPerdido);
        reporte.Serie                  = ReportAggregation.Serie(p, granularidad,
            perdidas.Select(x => (x.Fecha, x.Unidades, x.Valor)));
        reporte.PorInsumo              = porInsumo.OrderByDescending(x => x.Valor).ToList();
        reporte.PorMotivo              = ReportAggregation.Corte(perdidas.Select(x => (x.Motivo, x.Valor)));

        return reporte;
    }

    // ── Stock muerto / sin rotación ───────────────────────────────
    public async Task<DeadStockReportDto> StockMuertoAsync(int dias)
    {
        if (dias < 1)   dias = 1;
        if (dias > 730) dias = 730;

        var corte = DateTime.UtcNow.AddDays(-dias);
        var ahora = DateTime.UtcNow;

        var insumos = (await _context.InventoryItems
            .Where(i => i.Activo && i.StockActual > 0)
            .Select(i => new
            {
                i.Id, i.Nombre, i.UnidadMedida, i.StockActual, i.PrecioCosto,
                Ultimo = i.InventoryMovements
                    .OrderByDescending(m => m.FechaHora)
                    .Select(m => (DateTime?)m.FechaHora)
                    .FirstOrDefault()
            })
            .ToListAsync())
            .Where(i => i.Ultimo == null || i.Ultimo < corte)
            .Select(i => new DeadStockItemDto
            {
                InventoryItemId  = i.Id,
                Nombre           = i.Nombre,
                UnidadMedida     = i.UnidadMedida,
                StockActual      = i.StockActual,
                PrecioCosto      = i.PrecioCosto,
                CapitalDetenido  = Math.Round(i.StockActual * i.PrecioCosto, 2),
                UltimoMovimiento = i.Ultimo,
                DiasSinMover     = i.Ultimo.HasValue ? (int)(ahora - i.Ultimo.Value).TotalDays : null
            })
            .OrderByDescending(i => i.CapitalDetenido)
            .ToList();

        var productos = (await _context.Products
            .Where(pr => pr.Activo && pr.Estado == "ACTIVO")
            .Select(pr => new
            {
                pr.Id, pr.Nombre, pr.PrecioBase,
                Ultima = pr.OrderItems
                    .Where(oi => oi.Order.EstadoPedido != "CANCELADO")
                    .OrderByDescending(oi => oi.Order.FechaCreacion)
                    .Select(oi => (DateTime?)oi.Order.FechaCreacion)
                    .FirstOrDefault()
            })
            .ToListAsync())
            .Where(pr => pr.Ultima == null || pr.Ultima < corte)
            .Select(pr => new DeadStockProductDto
            {
                ProductId     = pr.Id,
                Nombre        = pr.Nombre,
                PrecioBase    = pr.PrecioBase,
                UltimaVenta   = pr.Ultima,
                DiasSinVender = pr.Ultima.HasValue ? (int)(ahora - pr.Ultima.Value).TotalDays : null
            })
            .OrderByDescending(pr => pr.DiasSinVender ?? int.MaxValue)
            .ToList();

        return new DeadStockReportDto
        {
            DiasSinMovimiento   = dias,
            CapitalInmovilizado = Math.Round(insumos.Sum(i => i.CapitalDetenido), 2),
            Insumos             = insumos,
            Productos           = productos
        };
    }
}
