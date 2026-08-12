using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Reports;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Services.Reports;

/// <summary>
/// Reportes de la categoría Ventas: panorama de ventas, rentabilidad por
/// producto y estacionalidad por festividad.
///
/// Todas las consultas proyectan solo las columnas necesarias y agrupan en
/// memoria por FECHA LOCAL. Agrupar en SQL exigiría traducir el desplazamiento
/// horario dentro de la consulta, y el volumen de una florería (miles de
/// pedidos, no millones) no lo justifica.
/// </summary>
public class SalesReportsService
{
    private readonly AppDbContext _context;

    public SalesReportsService(AppDbContext context) => _context = context;

    // ── Panorama de ventas ────────────────────────────────────────
    public async Task<SalesOverviewDto> PanoramaAsync(ReportPeriod p, ReportGranularity granularidad)
    {
        var actual   = await CargarPedidosAsync(p);
        var anterior = await CargarPedidosAsync(p.Anterior);

        var vigentes         = actual.Where(o => o.Estado != "CANCELADO").ToList();
        var vigentesAnterior = anterior.Where(o => o.Estado != "CANCELADO").ToList();

        var totalVentas  = vigentes.Sum(o => o.Total);
        var totalPedidos = vigentes.Count;
        var ticket       = totalPedidos > 0 ? Math.Round(totalVentas / totalPedidos, 2) : 0m;

        var ventasAnterior  = vigentesAnterior.Sum(o => o.Total);
        var pedidosAnterior = vigentesAnterior.Count;
        var ticketAnterior  = pedidosAnterior > 0 ? Math.Round(ventasAnterior / pedidosAnterior, 2) : 0m;

        // Pagos registrados dentro del periodo (dinero que efectivamente entró).
        var pagos = await _context.Payments
            .Where(pg => pg.FechaPago >= p.DesdeUtc && pg.FechaPago < p.HastaUtcExclusivo &&
                         pg.Estado != "CANCELADO")
            .Select(pg => new { pg.Monto, pg.Metodo })
            .ToListAsync();

        return new SalesOverviewDto
        {
            TotalPedidos      = totalPedidos,
            TotalVentas       = totalVentas,
            TicketPromedio    = ticket,
            TotalCobrado      = pagos.Sum(x => x.Monto),
            SaldoPorCobrar    = vigentes.Sum(o => o.SaldoPendiente),
            PedidosCancelados = actual.Count - totalPedidos,

            VentasVsAnterior  = ComparisonDto.De(totalVentas, ventasAnterior),
            PedidosVsAnterior = ComparisonDto.De(totalPedidos, pedidosAnterior),
            TicketVsAnterior  = ComparisonDto.De(ticket, ticketAnterior),

            Granularidad = granularidad.ToString().ToLowerInvariant(),
            Serie = ReportAggregation.Serie(
                p, granularidad,
                vigentes.Select(o => (o.FechaLocal, 1, o.Total))),

            PorCanal = ReportAggregation.Corte(
                vigentes.Select(o => (Normalizar(o.Canal), o.Total))),

            PorTipo = ReportAggregation.Corte(
                vigentes.Select(o => (Normalizar(o.Tipo), o.Total))),

            PorMetodoPago = ReportAggregation.Corte(
                pagos.Select(x => (Normalizar(x.Metodo), x.Monto)))
        };
    }

    private sealed record OrderRow(DateOnly FechaLocal, decimal Total, decimal SaldoPendiente,
                                   string Canal, string Tipo, string Estado);

    private async Task<List<OrderRow>> CargarPedidosAsync(ReportPeriod p)
    {
        var filas = await _context.Orders
            .Where(o => o.FechaCreacion >= p.DesdeUtc && o.FechaCreacion < p.HastaUtcExclusivo)
            .Select(o => new { o.FechaCreacion, o.Total, o.SaldoPendiente, o.Canal, o.TipoPedido, o.EstadoPedido })
            .ToListAsync();

        return filas
            .Select(o => new OrderRow(ReportPeriod.ALocal(o.FechaCreacion), o.Total, o.SaldoPendiente,
                                      o.Canal, o.TipoPedido, o.EstadoPedido))
            .ToList();
    }

    private static string Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? "SIN ESPECIFICAR" : valor.Trim().ToUpperInvariant();

    // ── Rentabilidad y margen por producto ────────────────────────
    public async Task<ProfitabilityReportDto> RentabilidadAsync(ReportPeriod p)
    {
        // Unidades e ingresos por producto en el periodo.
        var vendidos = await _context.OrderItems
            .Where(oi => oi.Order.FechaCreacion >= p.DesdeUtc &&
                         oi.Order.FechaCreacion <  p.HastaUtcExclusivo &&
                         oi.Order.EstadoPedido  != "CANCELADO")
            .GroupBy(oi => new { oi.ProductId, oi.Product.Nombre })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.Nombre,
                Unidades = g.Sum(oi => oi.Cantidad),
                Ingresos = g.Sum(oi => oi.Subtotal)
            })
            .ToListAsync();

        if (vendidos.Count == 0) return new ProfitabilityReportDto();

        var ids = vendidos.Select(v => v.ProductId).ToList();

        // Costo unitario = Σ (cantidad requerida × precio de costo del insumo).
        // Solo cuentan los insumos marcados con SumaAlCosto: los consumibles que
        // el negocio decidió no costear (papel, listón de cortesía) quedan fuera.
        var costos = await _context.ProductRecipes
            .Where(r => ids.Contains(r.ProductId) && r.InventoryItem.SumaAlCosto)
            .GroupBy(r => r.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Costo     = g.Sum(r => r.CantidadRequerida * r.InventoryItem.PrecioCosto)
            })
            .ToListAsync();

        // Un producto puede tener receta compuesta solo de insumos con SumaAlCosto = false;
        // en ese caso sí tiene receta y su costo es 0, no "desconocido".
        var conReceta = (await _context.ProductRecipes
            .Where(r => ids.Contains(r.ProductId))
            .Select(r => r.ProductId)
            .Distinct()
            .ToListAsync()).ToHashSet();

        var costoPorProducto = costos.ToDictionary(c => c.ProductId, c => c.Costo);

        var productos = new List<ProductProfitDto>();
        foreach (var v in vendidos)
        {
            var tieneReceta   = conReceta.Contains(v.ProductId);
            decimal? costoUni = tieneReceta ? costoPorProducto.GetValueOrDefault(v.ProductId, 0m) : null;
            decimal? costoTot = costoUni.HasValue ? costoUni.Value * v.Unidades : null;
            decimal? margen   = costoTot.HasValue ? v.Ingresos - costoTot.Value : null;

            productos.Add(new ProductProfitDto
            {
                ProductId      = v.ProductId,
                Nombre         = v.Nombre,
                Unidades       = v.Unidades,
                Ingresos       = v.Ingresos,
                PrecioPromedio = v.Unidades > 0 ? Math.Round(v.Ingresos / v.Unidades, 2) : 0m,
                TieneReceta    = tieneReceta,
                CostoUnitario  = costoUni.HasValue ? Math.Round(costoUni.Value, 2) : null,
                CostoTotal     = costoTot.HasValue ? Math.Round(costoTot.Value, 2) : null,
                Margen         = margen.HasValue ? Math.Round(margen.Value, 2) : null,
                MargenPct      = margen.HasValue && v.Ingresos > 0
                    ? Math.Round(margen.Value / v.Ingresos * 100, 1)
                    : null
            });
        }

        var costeados      = productos.Where(x => x.TieneReceta).ToList();
        var ingresosTotal  = productos.Sum(x => x.Ingresos);
        var costoTotal     = costeados.Sum(x => x.CostoTotal ?? 0m);
        var ingresosCost   = costeados.Sum(x => x.Ingresos);
        var margenTotal    = ingresosCost - costoTotal;

        return new ProfitabilityReportDto
        {
            IngresosTotales    = ingresosTotal,
            CostoTotal         = Math.Round(costoTotal, 2),
            MargenTotal        = Math.Round(margenTotal, 2),
            MargenPct          = ingresosCost > 0 ? Math.Round(margenTotal / ingresosCost * 100, 1) : null,
            ProductosSinReceta = productos.Count - costeados.Count,
            IngresosSinCostear = productos.Where(x => !x.TieneReceta).Sum(x => x.Ingresos),
            Productos          = productos.OrderByDescending(x => x.Margen ?? decimal.MinValue)
                                          .ThenByDescending(x => x.Ingresos)
                                          .ToList()
        };
    }

    // ── Estacionalidad por festividad ─────────────────────────────
    public async Task<SeasonalityReportDto> EstacionalidadAsync(int anios)
    {
        if (anios < 1) anios = 1;
        if (anios > 6) anios = 6;

        var catalogos = await _context.Catalogos
            .Where(c => c.MesDiaInicio != null && c.MesDiaFin != null)
            .Select(c => new { c.Id, c.Nombre, c.MesDiaInicio, c.MesDiaFin })
            .ToListAsync();

        if (catalogos.Count == 0) return new SeasonalityReportDto();

        var hoy      = ReportPeriod.HoyLocal;
        var anioBase = hoy.Year;

        // Ventana global: desde el 1 de enero del año más viejo que se compara.
        // Se carga una sola vez y cada festividad recorta lo suyo en memoria.
        var global = new ReportPeriod(new DateOnly(anioBase - anios + 1, 1, 1), hoy);

        var pedidos = (await _context.Orders
            .Where(o => o.FechaCreacion >= global.DesdeUtc && o.FechaCreacion < global.HastaUtcExclusivo &&
                        o.EstadoPedido != "CANCELADO")
            .Select(o => new { o.Id, o.FechaCreacion, o.Total })
            .ToListAsync())
            .Select(o => new { o.Id, Fecha = ReportPeriod.ALocal(o.FechaCreacion), o.Total })
            .ToList();

        var lineas = (await _context.OrderItems
            .Where(oi => oi.Order.FechaCreacion >= global.DesdeUtc &&
                         oi.Order.FechaCreacion <  global.HastaUtcExclusivo &&
                         oi.Order.EstadoPedido  != "CANCELADO")
            .Select(oi => new { oi.ProductId, oi.Cantidad, oi.Subtotal, oi.Order.FechaCreacion })
            .ToListAsync())
            .Select(x => new { x.ProductId, x.Cantidad, x.Subtotal, Fecha = ReportPeriod.ALocal(x.FechaCreacion) })
            .ToList();

        var salidas = (await _context.InventoryMovements
            .Where(m => m.TipoMovimiento == "SALIDA" &&
                        m.FechaHora >= global.DesdeUtc && m.FechaHora < global.HastaUtcExclusivo)
            .Select(m => new { m.Cantidad, m.FechaHora })
            .ToListAsync())
            .Select(m => new { m.Cantidad, Fecha = ReportPeriod.ALocal(m.FechaHora) })
            .ToList();

        var productosPorCatalogo = (await _context.ProductCatalogos
            .Select(pc => new { pc.CatalogoId, pc.ProductId })
            .ToListAsync())
            .GroupBy(x => x.CatalogoId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ProductId).ToHashSet());

        var resultado = new SeasonalityReportDto();

        foreach (var cat in catalogos)
        {
            var festividad = new FestivityDto
            {
                CatalogoId   = cat.Id,
                Nombre       = cat.Nombre,
                MesDiaInicio = cat.MesDiaInicio,
                MesDiaFin    = cat.MesDiaFin
            };

            var delCatalogo = productosPorCatalogo.GetValueOrDefault(cat.Id) ?? [];

            for (var offset = anios - 1; offset >= 0; offset--)
            {
                var anio = anioBase - offset;
                var ventana = VentanaDe(cat.MesDiaInicio!, cat.MesDiaFin!, anio);
                if (ventana is null) continue;

                var (inicio, fin) = ventana.Value;
                // El año en curso puede tener la ventana todavía por delante.
                if (inicio > hoy) continue;

                var pedidosVentana = pedidos.Where(o => o.Fecha >= inicio && o.Fecha <= fin).ToList();
                var lineasVentana  = lineas .Where(l => l.Fecha >= inicio && l.Fecha <= fin).ToList();

                festividad.Anios.Add(new FestivityYearDto
                {
                    Anio                = anio,
                    Inicio              = inicio,
                    Fin                 = fin,
                    Pedidos             = pedidosVentana.Count,
                    VentasTotales       = pedidosVentana.Sum(o => o.Total),
                    UnidadesDelCatalogo = lineasVentana.Where(l => delCatalogo.Contains(l.ProductId)).Sum(l => l.Cantidad),
                    VentasDelCatalogo   = lineasVentana.Where(l => delCatalogo.Contains(l.ProductId)).Sum(l => l.Subtotal),
                    ConsumoInsumos      = salidas.Where(m => m.Fecha >= inicio && m.Fecha <= fin).Sum(m => m.Cantidad)
                });
            }

            if (festividad.Anios.Count > 0) resultado.Festividades.Add(festividad);
        }

        resultado.Festividades = resultado.Festividades
            .OrderByDescending(f => f.Anios.LastOrDefault()?.VentasTotales ?? 0m)
            .ToList();

        return resultado;
    }

    /// <summary>
    /// Traduce la ventana "MM-DD" a fechas de un año concreto. Si la ventana
    /// cruza el fin de año (ej. 12-28 → 01-05) el cierre cae en el año siguiente.
    /// </summary>
    private static (DateOnly Inicio, DateOnly Fin)? VentanaDe(string mesDiaInicio, string mesDiaFin, int anio)
    {
        if (!TryParseMesDia(mesDiaInicio, anio, out var inicio)) return null;
        if (!TryParseMesDia(mesDiaFin,    anio, out var fin))    return null;
        if (fin < inicio) fin = fin.AddYears(1);
        return (inicio, fin);
    }

    private static bool TryParseMesDia(string valor, int anio, out DateOnly fecha)
    {
        fecha = default;
        var partes = valor.Split('-');
        if (partes.Length != 2) return false;
        if (!int.TryParse(partes[0], out var mes) || !int.TryParse(partes[1], out var dia)) return false;
        if (mes is < 1 or > 12) return false;
        if (dia < 1 || dia > DateTime.DaysInMonth(anio, mes)) return false;
        fecha = new DateOnly(anio, mes, dia);
        return true;
    }
}
