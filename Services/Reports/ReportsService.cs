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
        var pedidos = await _context.Orders
            .Where(o => o.FechaEntrega >= desde &&
                        o.FechaEntrega <= hasta &&
                        o.EstadoPedido != "CANCELADO")
            .ToListAsync();

        var ventasPorDia = pedidos
            .GroupBy(o => o.FechaEntrega)
            .OrderBy(g => g.Key)
            .Select(g => new DailySaleDto
            {
                Fecha   = g.Key,
                Pedidos = g.Count(),
                Total   = g.Sum(o => o.Total)
            }).ToList();

        var totalVentas = pedidos.Sum(o => o.Total);

        return new SalesReportDto
        {
            Desde         = desde,
            Hasta         = hasta,
            TotalPedidos  = pedidos.Count,
            TotalVentas   = totalVentas,
            PromedioVenta = pedidos.Count > 0 ? totalVentas / pedidos.Count : 0,
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
    public async Task<DashboardStatsDto> ObtenerDashboardStatsAsync()
    {
        var hoy   = DateOnly.FromDateTime(DateTime.UtcNow);
        var semanaPasada = hoy.AddDays(-7);

        // 1. Pedidos (últimos 30 días para KPIs generales)
        var inicioMes = hoy.AddDays(-30);
        var pedidosMes = await _context.Orders
            .Where(o => o.FechaEntrega >= inicioMes && o.EstadoPedido != "CANCELADO")
            .ToListAsync();

        var totalSales  = pedidosMes.Sum(o => o.Total);
        var orderCount  = pedidosMes.Count;
        var avgTicket   = orderCount > 0 ? totalSales / orderCount : 0;

        // 2. Nuevos clientes (últimos 7 días)
        var newCustomers = await _context.Users
            .Where(u => u.CreadoEn >= DateTime.UtcNow.AddDays(-7))
            .CountAsync();

        // 3. Ventas semanales (para la gráfica)
        var deHoyParaAtras = await _context.Orders
            .Where(o => o.FechaEntrega >= semanaPasada && o.EstadoPedido != "CANCELADO")
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
            WeeklySales       = deHoyParaAtras,
            CriticalInventory = criticalInventory
        };
    }
}
