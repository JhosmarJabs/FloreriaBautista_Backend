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
        var items = await _context.InventoryItems
            .Include(i => i.Product)
            .Where(i => i.Product.Estado == "ACTIVO")
            .ToListAsync();

        var bajoStock = items.Where(i => i.StockActual <= i.StockMinimo && i.StockActual > 0).ToList();
        var sinStock  = items.Where(i => i.StockActual == 0).ToList();

        return new InventoryReportDto
        {
            TotalProductos     = items.Count,
            ProductosBajoStock = bajoStock.Count,
            ProductosSinStock  = sinStock.Count,
            BajoStock = bajoStock.Concat(sinStock)
                .OrderBy(i => i.StockActual)
                .Select(i => new LowStockDto
                {
                    ProductId   = i.ProductId,
                    Nombre      = i.Product.Nombre,
                    StockActual = i.StockActual,
                    StockMinimo = i.StockMinimo
                }).ToList()
        };
    }
}
