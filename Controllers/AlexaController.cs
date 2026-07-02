using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using FloreriaBautista.Data;
using FloreriaBautista.Services.Reports;
using FloreriaBautista.Services.Interfaces;
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
    private readonly IInventoryService _inventoryService;

    public AlexaController(AppDbContext context, ReportsService reportsService, IInventoryService inventoryService)
    {
        _context = context;
        _reportsService = reportsService;
        _inventoryService = inventoryService;
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
            query = query.Where(i => i.Nombre.ToLower().Contains(busqueda.ToLower()));
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

    // GET /api/alexa/inventory/resolver
    [HttpGet("inventory/resolver")]
    public async Task<IActionResult> ResolverInsumo([FromQuery] string termino)
    {
        if (string.IsNullOrWhiteSpace(termino))
        {
            return BadRequest(ApiResponseDto<object>.Fail("El término de búsqueda es requerido."));
        }

        var item = await _inventoryService.ResolverCoincidenciaInsumoAsync(termino);
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

        var stats = await _context.Orders
            .Where(o => o.FechaEntrega == hoy && o.EstadoPedido != "CANCELADO" && !o.Archivado)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalVendido = g.Sum(o => o.Total),
                TotalPedidos = g.Count()
            })
            .FirstOrDefaultAsync();

        var totalVendido = stats?.TotalVendido ?? 0;
        var totalPedidos = stats?.TotalPedidos ?? 0;

        return Ok(new
        {
            Fecha          = hoy,
            TotalVendido   = totalVendido,
            TotalPedidos   = totalPedidos,
            TicketPromedio = totalPedidos > 0 ? totalVendido / totalPedidos : 0
        });
    }

    // GET /api/alexa/orders/pendientes
    [HttpGet("orders/pendientes")]
    public async Task<IActionResult> GetPedidosPendientesHoy()
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var pendientes = await _context.Orders
            .Where(o => o.FechaEntrega == hoy &&
                        !o.Archivado &&
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

    // GET /api/alexa/dashboard?periodo=dia|semana|mes
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboardStats([FromQuery] string periodo = "mes")
    {
        var stats = await _reportsService.ObtenerDashboardStatsAsync(periodo);
        return Ok(stats);
    }

    // POST /api/alexa/reabastecer
    [HttpPost("reabastecer")]
    public async Task<IActionResult> EnviarSolicitudReabastecimiento()
    {
        try
        {
            // Obtener productos con stock bajo
            var productosCortos = await _context.InventoryItems
                .Where(i => i.Activo && i.StockActual <= i.StockMinimo)
                .OrderBy(i => i.Nombre)
                .Select(i => new
                {
                    i.Nombre,
                    i.StockActual,
                    i.StockMinimo
                })
                .ToListAsync();

            if (!productosCortos.Any())
            {
                return Ok(ApiResponseDto<object>.Fail("No hay productos con stock bajo para reabastecer."));
            }

            // Generar mensaje formateado
            var mensaje = GenerarMensajeReabastecimiento(productosCortos);

            // Determinar ambiente (TEST o PROD)
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production" ? "prod" : "test";
            var n8nUrl = environment == "test"
                ? "https://edith-n8n.btxyoq.easypanel.host/webhook-test/reabastecer"
                : "https://edith-n8n.btxyoq.easypanel.host/webhook/reabastecer";

            // Preparar payload para n8n
            var payload = new
            {
                mensaje = mensaje,
                environment = environment
            };

            // Enviar a n8n
            var response = await EnviarAn8nAsync(n8nUrl, payload);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode(500, ApiResponseDto<object>.Fail("Error enviando solicitud a n8n."));
            }

            return Ok(ApiResponseDto<object>.Ok(new
            {
                mensaje = "Solicitud de reabastecimiento enviada correctamente",
                productosCortos = productosCortos.Count(),
                ambiente = environment
            }));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponseDto<object>.Fail($"Error: {ex.Message}"));
        }
    }

    // Método privado para generar el mensaje formateado
    private string GenerarMensajeReabastecimiento(IEnumerable<dynamic> productosCortos)
    {
        var mensaje = "📦 REABASTECER - Insumos sin stock:\n\n";

        foreach (var producto in productosCortos)
        {
            mensaje += $"- {producto.Nombre}: {producto.StockActual} unidades (Mínimo: {producto.StockMinimo})\n";
        }

        mensaje += "\n✅ ¿Confirmas que solicite esto a tu proveedor?";

        return mensaje;
    }

    // Método privado para enviar a n8n
    private async Task<HttpResponseMessage> EnviarAn8nAsync(string url, object payload)
    {
        using (var client = new HttpClient())
        {
            client.Timeout = TimeSpan.FromSeconds(10);

            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            try
            {
                return await client.PostAsync(url, content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[n8n Error] {ex.Message}");
                throw;
            }
        }
    }
}
