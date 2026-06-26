using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using FloreriaBautista.Data;
using FloreriaBautista.Services.Reports;

namespace FloreriaBautista.Controllers;

[ApiController]
[Tags("Alexa Integration")]
[Route("api/alexa")]
[Authorize(Roles = "ADMIN")]
public class AlexaController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ReportsService _reportsService;

    // Caché en memoria de productos activos (ID y Nombre)
    private static List<(Guid Id, string Nombre)>? _productosEnMemoria;
    private static readonly object _lock = new object();

    public AlexaController(AppDbContext context, ReportsService reportsService)
    {
        _context = context;
        _reportsService = reportsService;
    }

    private async Task AsegurarCacheProductosAsync(bool forzarRefresco = false)
    {
        if (_productosEnMemoria == null || forzarRefresco)
        {
            var items = await _context.Products
                .Where(p => p.Estado == "ACTIVO")
                .Select(p => new { p.Id, p.Nombre })
                .ToListAsync();

            lock (_lock)
            {
                _productosEnMemoria = items.Select(x => (x.Id, x.Nombre)).ToList();
            }
        }
    }

    // GET /api/alexa/products?busqueda=rosa
    [HttpGet("products")]
    public async Task<IActionResult> GetProducts([FromQuery] string? busqueda)
    {
        // Si no hay parámetro de búsqueda, forzamos la actualización del caché en memoria
        bool forzarRefresco = string.IsNullOrWhiteSpace(busqueda);
        await AsegurarCacheProductosAsync(forzarRefresco);

        var query = _context.Products.Where(p => p.Estado == "ACTIVO");
        
        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            // Filtrar en memoria los productos que coincidan parcialmente con la búsqueda para obtener sus IDs
            var idsCoincidentes = _productosEnMemoria!
                .Where(p => p.Nombre.Contains(busqueda, StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Id)
                .ToList();

            // Consultar a la base de datos por ID en lugar de filtrar por texto
            query = query.Where(p => idsCoincidentes.Contains(p.Id));
        }

        var items = await query
            .OrderBy(p => p.Nombre)
            .Select(p => new 
            { 
                p.Id, 
                p.Nombre, 
                p.PrecioBase, 
                p.ImagenUrl 
            })
            .ToListAsync();

        return Ok(items);
    }

    // GET /api/alexa/products/{id:guid}
    [HttpGet("products/{id:guid}")]
    public async Task<IActionResult> GetProduct(Guid id)
    {
        var product = await _context.Products
            .Where(p => p.Id == id)
            .Select(p => new 
            { 
                p.Id, 
                p.Nombre, 
                p.Descripcion, 
                p.PrecioBase, 
                p.Estado, 
                p.ImagenUrl 
            })
            .FirstOrDefaultAsync();

        if (product == null)
        {
            return NotFound(new { mensaje = "Producto no encontrado." });
        }

        return Ok(product);
    }

    // GET /api/alexa/reports/ventas-hoy
    [HttpGet("reports/ventas-hoy")]
    public async Task<IActionResult> GetVentasHoy()
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var pedidosHoy = await _context.Orders
            .Where(o => o.FechaEntrega == hoy && o.EstadoPedido != "CANCELADO")
            .ToListAsync();

        var totalVendido = pedidosHoy.Sum(o => o.Total);
        var totalPedidos = pedidosHoy.Count;
        var promedioTicket = totalPedidos > 0 ? totalVendido / totalPedidos : 0;

        return Ok(new
        {
            Fecha = hoy,
            TotalVendido = totalVendido,
            TotalPedidos = totalPedidos,
            TicketPromedio = promedioTicket
        });
    }

    // GET /api/alexa/orders/pendientes
    [HttpGet("orders/pendientes")]
    public async Task<IActionResult> GetPedidosPendientesHoy()
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var pendientes = await _context.Orders
            .Include(o => o.Customer)
            .Where(o => o.FechaEntrega == hoy && 
                        o.EstadoPedido != "ENTREGADO" && 
                        o.EstadoPedido != "CANCELADO" &&
                        o.EstadoPedido != "ENTREGADA" &&
                        o.EstadoPedido != "CANCELADA")
            .OrderBy(o => o.HoraEntrega)
            .Select(o => new
            {
                o.Id,
                Cliente = o.Customer.Nombre,
                o.EstadoPedido,
                o.HoraEntrega,
                o.Total,
                Direccion = $"{o.DireccionEntregaCalle}, {o.DireccionEntregaColonia}, {o.DireccionEntregaMunicipio}".Trim(' ', ',')
            })
            .ToListAsync();

        return Ok(pendientes);
    }

    // GET /api/alexa/inventory/bajo-stock
    [HttpGet("inventory/bajo-stock")]
    public async Task<IActionResult> GetBajoStock()
    {
        var items = await _context.InventoryItems
            .Where(i => i.Activo && i.StockActual <= i.StockMinimo)
            .Select(i => new
            {
                i.Id,
                i.Nombre,
                i.StockActual,
                i.StockMinimo,
                i.UnidadMedida
            })
            .ToListAsync();

        return Ok(items);
    }

    // GET /api/alexa/dashboard
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboardStats()
    {
        var stats = await _reportsService.ObtenerDashboardStatsAsync();
        return Ok(stats);
    }
}
