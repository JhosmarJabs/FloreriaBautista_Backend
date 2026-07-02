using Microsoft.EntityFrameworkCore;
using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Reports;

namespace FloreriaBautista.Services.Reports;

public class ReportsService
{
    private readonly AppDbContext             _context;
    private readonly ILogger<ReportsService>  _logger;

    public ReportsService(AppDbContext context, ILogger<ReportsService> logger)
    {
        _context = context;
        _logger  = logger;
    }

    // ── Ventas ────────────────────────────────────────────────────
    public async Task<SalesReportDto> ReporteVentasAsync(DateOnly desde, DateOnly hasta)
    {
        var baseQuery = _context.Orders
            .Where(o => o.FechaEntrega >= desde &&
                        o.FechaEntrega <= hasta &&
                        o.EstadoPedido != "CANCELADO");

        // Totales en SQL, sin traer registros individuales a memoria
        var totales = await baseQuery
            .GroupBy(_ => 1)
            .Select(g => new { TotalPedidos = g.Count(), TotalVentas = g.Sum(o => o.Total) })
            .FirstOrDefaultAsync();

        var totalPedidos = totales?.TotalPedidos ?? 0;
        var totalVentas  = totales?.TotalVentas  ?? 0m;

        // Agrupación diaria en SQL
        var ventasPorDia = await baseQuery
            .GroupBy(o => o.FechaEntrega)
            .OrderBy(g => g.Key)
            .Select(g => new DailySaleDto
            {
                Fecha   = g.Key,
                Pedidos = g.Count(),
                Total   = g.Sum(o => o.Total)
            })
            .ToListAsync();

        return new SalesReportDto
        {
            Desde         = desde,
            Hasta         = hasta,
            TotalPedidos  = totalPedidos,
            TotalVentas   = totalVentas,
            PromedioVenta = totalPedidos > 0 ? totalVentas / totalPedidos : 0,
            VentasPorDia  = ventasPorDia
        };
    }

    // ── Top productos ─────────────────────────────────────────────
    public async Task<List<TopProductDto>> TopProductosAsync(DateOnly? desde, DateOnly? hasta, int top = 10)
    {
        var query = _context.OrderItems
            .Include(oi => oi.Product)
            .Include(oi => oi.Order)
            .Where(oi => oi.Order.EstadoPedido != "CANCELADO")
            .AsQueryable();

        if (desde.HasValue) query = query.Where(oi => oi.Order.FechaEntrega >= desde.Value);
        if (hasta.HasValue) query = query.Where(oi => oi.Order.FechaEntrega <= hasta.Value);

        return await query
            .GroupBy(oi => new { oi.ProductId, oi.Product.Nombre })
            .Select(g => new TopProductDto
            {
                ProductId = g.Key.ProductId,
                Nombre    = g.Key.Nombre,
                Vendidos  = g.Sum(oi => oi.Cantidad),
                Ingresos  = g.Sum(oi => oi.Subtotal)
            })
            .OrderByDescending(p => p.Vendidos)
            .Take(top)
            .ToListAsync();
    }

    // ── Top clientes ──────────────────────────────────────────────
    public async Task<List<TopCustomerDto>> TopClientesAsync(DateOnly? desde, DateOnly? hasta, int top = 10)
    {
        var query = _context.Orders
            .Include(o => o.Customer)
            .Where(o => o.EstadoPedido != "CANCELADO")
            .AsQueryable();

        if (desde.HasValue) query = query.Where(o => o.FechaEntrega >= desde.Value);
        if (hasta.HasValue) query = query.Where(o => o.FechaEntrega <= hasta.Value);

        return await query
            .GroupBy(o => new { o.CustomerId, o.Customer.Nombre })
            .Select(g => new TopCustomerDto
            {
                CustomerId   = g.Key.CustomerId,
                Nombre       = g.Key.Nombre,
                Pedidos      = g.Count(),
                TotalGastado = g.Sum(o => o.Total)
            })
            .OrderByDescending(c => c.TotalGastado)
            .Take(top)
            .ToListAsync();
    }

    // ── Inventario ────────────────────────────────────────────────
    public async Task<InventoryReportDto> ReporteInventarioAsync()
    {
        var items = await _context.InventoryItems.ToListAsync();

        var bajoStock = items.Where(i => i.StockActual <= i.StockMinimo && i.StockActual > 0).ToList();
        var sinStock  = items.Where(i => i.StockActual == 0).ToList();

        return new InventoryReportDto
        {
            TotalInsumos       = items.Count,
            InsumosBajoStock   = bajoStock.Count,
            InsumosSinStock    = sinStock.Count,
            BajoStock = bajoStock.Concat(sinStock)
                .OrderBy(i => i.StockActual)
                .Select(i => new LowStockDto
                {
                    ItemId      = i.Id,
                    Nombre      = i.Nombre,
                    StockActual = i.StockActual,
                    StockMinimo = i.StockMinimo
                }).ToList()
        };
    }

    // ── Dashboard ─────────────────────────────────────────────────
    public async Task<DashboardStatsDto> ObtenerDashboardStatsAsync(string periodo = "mes")
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        // Rango según el periodo que envía Alexa (dia / semana / mes)
        var dias = periodo switch
        {
            "dia"    => 1,
            "semana" => 7,
            _        => 30   // "mes" por defecto
        };
        var desde = hoy.AddDays(-(dias - 1)); // incluye hoy

        // 1. Pedidos del periodo
        var pedidos = await _context.Orders
            .Where(o => o.FechaEntrega >= desde && o.FechaEntrega <= hoy && o.EstadoPedido != "CANCELADO")
            .ToListAsync();

        var totalSales  = pedidos.Sum(o => o.Total);
        var orderCount  = pedidos.Count;
        var avgTicket   = orderCount > 0 ? totalSales / orderCount : 0;

        // 2. Nuevos clientes en el mismo periodo
        var desdeDateTime = desde.ToDateTime(TimeOnly.MinValue);
        var newCustomers = await _context.Users
            .Where(u => u.CreadoEn >= desdeDateTime)
            .CountAsync();

        // 3. Ventas por día del periodo (para la gráfica)
        var ventasPorDia = await _context.Orders
            .Where(o => o.FechaEntrega >= desde && o.FechaEntrega <= hoy && o.EstadoPedido != "CANCELADO")
            .GroupBy(o => o.FechaEntrega)
            .Select(g => new DailySaleDto
            {
                Fecha   = g.Key,
                Pedidos = g.Count(),
                Total   = g.Sum(o => o.Total)
            })
            .OrderBy(d => d.Fecha)
            .ToListAsync();

        // 4. Inventario crítico
        var criticalInventory = await _context.InventoryItems
            .Where(i => i.Activo && i.StockActual <= i.StockMinimo)
            .OrderBy(i => i.StockActual)
            .Take(10)
            .Select(i => new LowStockDto
            {
                ItemId      = i.Id,
                Nombre      = i.Nombre,
                StockActual = i.StockActual,
                StockMinimo = i.StockMinimo
            })
            .ToListAsync();

        return new DashboardStatsDto
        {
            TotalSales        = totalSales,
            OrderCount        = orderCount,
            AverageTicket     = avgTicket,
            NewCustomers      = newCustomers,
            MonthlySales      = ventasPorDia,
            CriticalInventory = criticalInventory
        };
    }
}
