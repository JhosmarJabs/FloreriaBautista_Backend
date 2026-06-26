using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using FloreriaBautista.Data;
using FloreriaBautista.Services.Reports;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Inventory;

namespace FloreriaBautista.Controllers;

[ApiController]
[Tags("Alexa Integration")]
[Route("api/alexa")]
[Authorize(Roles = "ADMIN")]
public class AlexaController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ReportsService _reportsService;

    // Caché en memoria de insumos activos (ID y Nombre)
    private static List<(Guid Id, string Nombre)>? _insumosEnMemoria;
    private static readonly object _lock = new object();

    public AlexaController(AppDbContext context, ReportsService reportsService)
    {
        _context = context;
        _reportsService = reportsService;
    }

    private async Task AsegurarCacheInsumosAsync(bool forzarRefresco = false)
    {
        if (_insumosEnMemoria == null || forzarRefresco)
        {
            var items = await _context.InventoryItems
                .Where(i => i.Activo)
                .Select(i => new { i.Id, i.Nombre })
                .ToListAsync();

            lock (_lock)
            {
                _insumosEnMemoria = items.Select(x => (x.Id, x.Nombre)).ToList();
            }
        }
    }

    // GET /api/alexa/inventory
    [HttpGet("inventory")]
    public async Task<IActionResult> GetInventory(
        [FromQuery] string? sucursal,
        [FromQuery] bool? bajoMinimo,
        [FromQuery] string? busqueda,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        bool forzarRefresco = string.IsNullOrWhiteSpace(busqueda);
        await AsegurarCacheInsumosAsync(forzarRefresco);

        var query = _context.InventoryItems.Where(i => i.Activo);

        if (!string.IsNullOrWhiteSpace(sucursal))
        {
            query = query.Where(i => i.Sucursal == sucursal);
        }

        if (bajoMinimo.HasValue && bajoMinimo.Value)
        {
            query = query.Where(i => i.StockActual <= i.StockMinimo);
        }

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var idsCoincidentes = _insumosEnMemoria!
                .Where(i => i.Nombre.Contains(busqueda, StringComparison.OrdinalIgnoreCase))
                .Select(i => i.Id)
                .ToList();

            query = query.Where(i => idsCoincidentes.Contains(i.Id));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(i => i.Nombre)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(i => new InventoryItemDto
            {
                Id = i.Id,
                Nombre = i.Nombre,
                StockActual = i.StockActual,
                StockMinimo = i.StockMinimo,
                Sucursal = i.Sucursal,
                SumaAlCosto = i.SumaAlCosto,
                UnidadMedida = i.UnidadMedida,
                PrecioCosto = i.PrecioCosto,
                EsFlorPrimaria = i.EsFlorPrimaria,
                ImagenUrl = i.ImagenUrl,
                Activo = i.Activo
            })
            .ToListAsync();

        var resultado = new PagedResultDto<InventoryItemDto>
        {
            Items = items,
            Total = total,
            Pagina = page,
            TamanoPagina = size,
            TotalPaginas = (int)Math.Ceiling((double)total / size)
        };

        return Ok(ApiResponseDto<PagedResultDto<InventoryItemDto>>.Ok(resultado));
    }

    // GET /api/alexa/inventory/{id:guid}
    [HttpGet("inventory/{id:guid}")]
    public async Task<IActionResult> GetInventoryItem(Guid id)
    {
        var item = await _context.InventoryItems
            .Where(i => i.Id == id)
            .Select(i => new InventoryItemDto
            {
                Id = i.Id,
                Nombre = i.Nombre,
                StockActual = i.StockActual,
                StockMinimo = i.StockMinimo,
                Sucursal = i.Sucursal,
                SumaAlCosto = i.SumaAlCosto,
                UnidadMedida = i.UnidadMedida,
                PrecioCosto = i.PrecioCosto,
                EsFlorPrimaria = i.EsFlorPrimaria,
                ImagenUrl = i.ImagenUrl,
                Activo = i.Activo
            })
            .FirstOrDefaultAsync();

        if (item == null)
        {
            return NotFound(ApiResponseDto<object>.Fail("Insumo no encontrado."));
        }

        return Ok(ApiResponseDto<InventoryItemDto>.Ok(item));
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
