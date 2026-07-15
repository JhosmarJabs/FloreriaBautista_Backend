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
        [FromQuery] int size = 20,
        [FromQuery] int diasConsumo = 30)
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

        var consumo = await _inventoryService.ObtenerConsumoRecienteAsync(diasConsumo);
        foreach (var item in items)
            item.UnidadesConsumidas = consumo.GetValueOrDefault(item.Id);

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
    public async Task<IActionResult> GetBajoStock([FromQuery] int diasConsumo = 30)
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

        var consumo = await _inventoryService.ObtenerConsumoRecienteAsync(diasConsumo);
        var resultado = items.Select(i => new
        {
            i.Id,
            i.Nombre,
            i.StockActual,
            i.StockMinimo,
            i.UnidadMedida,
            UnidadesConsumidas = consumo.GetValueOrDefault(i.Id)
        });

        return Ok(resultado);
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
    public async Task<IActionResult> EnviarSolicitudReabastecimiento([FromBody] ReabastecerRequest? req = null)
    {
        Console.WriteLine("\n" + new string('=', 80));
        Console.WriteLine("🔔 SOLICITUD DE REABASTECER INICIADA");
        Console.WriteLine(new string('=', 80));

        try
        {
            Console.WriteLine("📍 [PASO 1] Consultando productos con stock bajo...");

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

            Console.WriteLine($"✅ Productos encontrados: {productosCortos.Count()}");

            if (!productosCortos.Any())
            {
                Console.WriteLine("⚠️  No hay productos con stock bajo para reabastecer.");
                Console.WriteLine(new string('=', 80) + "\n");
                return Ok(ApiResponseDto<object>.Fail("No hay productos con stock bajo para reabastecer."));
            }

            // Listar productos
            Console.WriteLine("\n📦 PRODUCTOS SIN STOCK:");
            foreach (var prod in productosCortos)
            {
                Console.WriteLine($"   - {prod.Nombre}: {prod.StockActual} uds (Mínimo: {prod.StockMinimo})");
            }

            Console.WriteLine("\n📍 [PASO 2] Generando mensaje...");

            // Generar mensaje formateado
            var mensaje = GenerarMensajeReabastecimiento(productosCortos);
            Console.WriteLine($"✅ Mensaje generado ({mensaje.Length} caracteres)");

            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production" ? "prod" : "test";
            var n8nUrl = Environment.GetEnvironmentVariable("N8N_REABASTECER_WEBHOOK_URL")
                ?? (environment == "prod"
                    ? "https://edith-n8n.btxyoq.easypanel.host/webhook/reabastecer"
                    : "https://edith-n8n.btxyoq.easypanel.host/webhook-test/reabastecer");

            Console.WriteLine("\n📍 [PASO 3] Enviando a n8n...");
            Console.WriteLine($"   URL: {n8nUrl}");

            // Enviar al webhook de n8n (n8n se encarga de reenviarlo a Evolution API)
            var response = await EnviarAn8nAsync(n8nUrl, mensaje, environment);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"\n❌ ERROR EN N8N");
                Console.WriteLine($"   Código: {response.StatusCode}");
                Console.WriteLine($"   Respuesta: {errorContent}");
                Console.WriteLine(new string('=', 80) + "\n");
                return StatusCode(500, ApiResponseDto<object>.Fail($"Error enviando a n8n: {response.StatusCode}"));
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"\n✅ ¡ÉXITO! Solicitud enviada a n8n correctamente");
            Console.WriteLine($"   Status Code: {response.StatusCode}");
            Console.WriteLine($"   Respuesta: {responseContent}");

            Console.WriteLine("\n📍 [PASO 4] Preparando respuesta al cliente...");

            var resultado = new
            {
                mensaje = "Solicitud de reabastecimiento enviada por WhatsApp",
                productosCortos = productosCortos.Count(),
                ambiente = environment,
                timestamp = DateTime.UtcNow
            };

            Console.WriteLine($"✅ Respuesta preparada");
            Console.WriteLine(new string('=', 80) + "\n");

            return Ok(ApiResponseDto<object>.Ok(resultado));
        }
        catch (HttpRequestException hre)
        {
            Console.WriteLine($"\n❌ ERROR DE CONEXIÓN");
            Console.WriteLine($"   Tipo: {hre.GetType().Name}");
            Console.WriteLine($"   Mensaje: {hre.Message}");
            Console.WriteLine($"   Problema: No se puede conectar al webhook de n8n");
            Console.WriteLine(new string('=', 80) + "\n");
            return StatusCode(503, ApiResponseDto<object>.Fail($"No se puede conectar a n8n: {hre.Message}"));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ EXCEPCIÓN EN EL PROCESO");
            Console.WriteLine($"   Tipo: {ex.GetType().Name}");
            Console.WriteLine($"   Mensaje: {ex.Message}");
            Console.WriteLine($"   StackTrace: {ex.StackTrace}");
            Console.WriteLine(new string('=', 80) + "\n");
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

    // Método privado para enviar la solicitud al webhook de n8n
    // (n8n es quien se encarga de reenviar el mensaje a Evolution API/WhatsApp)
    private async Task<HttpResponseMessage> EnviarAn8nAsync(string url, string mensaje, string environment)
    {
        try
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(15);

                var payload = new
                {
                    mensaje = mensaje,
                    environment = environment
                };

                var json = System.Text.Json.JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                Console.WriteLine($"\n   📤 Enviando POST a n8n...");
                Console.WriteLine($"   URL: {url}");
                Console.WriteLine($"   Payload: {json}");

                var response = await client.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"\n   ✅ Respuesta exitosa (HTTP {response.StatusCode})");
                    Console.WriteLine($"   Content: {responseContent}");
                }
                else
                {
                    Console.WriteLine($"\n   ❌ Respuesta con error (HTTP {response.StatusCode})");
                    Console.WriteLine($"   Content: {responseContent}");
                }

                return response;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[n8n Exception] {ex.Message}");
            throw;
        }
    }
}

/// <summary>
/// Cuerpo del POST /api/alexa/reabastecer. La Skill de Alexa indica el ambiente
/// ("prod" o "test") para que el backend elija la URL de n8n correspondiente.
/// </summary>
public class ReabastecerRequest
{
    public string? Environment { get; set; }
}
