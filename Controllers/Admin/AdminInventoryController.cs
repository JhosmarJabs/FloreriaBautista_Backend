using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Inventory;
using FloreriaBautista.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FloreriaBautista.Controllers.Admin;

[ApiController]
[Tags("2. Operaciones y Ventas")]
[Route("api/admin/inventory")]
[Authorize(Roles = "ADMIN,EMPLEADO")]
public class AdminInventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;
    public AdminInventoryController(IInventoryService inventoryService) => _inventoryService = inventoryService;

    // GET /api/admin/inventory/kpis
    [HttpGet("kpis")]
    public async Task<IActionResult> ObtenerKpis()
    {
        var kpis = await _inventoryService.ObtenerKpisAsync();
        return Ok(ApiResponseDto<InventoryKpisDto>.Ok(kpis));
    }

    // GET /api/admin/inventory?sucursal=PRINCIPAL&bajoMinimo=true&busqueda=rosa
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string? sucursal,
        [FromQuery] bool?   bajoMinimo,
        [FromQuery] string? busqueda,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var resultado = await _inventoryService.ListarAsync(sucursal, bajoMinimo, busqueda, page, size);
        return Ok(ApiResponseDto<PagedResultDto<InventoryItemDto>>.Ok(resultado));
    }

    // GET /api/admin/inventory/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detalle(Guid id)
    {
        var item = await _inventoryService.ObtenerAsync(id);
        return Ok(ApiResponseDto<InventoryItemDto>.Ok(item));
    }

    // POST /api/admin/inventory
    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Crear([FromBody] CreateInventoryItemDto request)
    {
        var item = await _inventoryService.CrearAsync(request);
        return Ok(ApiResponseDto<InventoryItemDto>.Ok(item, "Insumo creado correctamente."));
    }

    // POST /api/admin/inventory/{id} (Actualizar / Borrado lógico)
    [HttpPost("{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] UpdateInventoryItemDto request)
    {
        var item = await _inventoryService.ActualizarAsync(id, request);
        return Ok(ApiResponseDto<InventoryItemDto>.Ok(item, "Insumo actualizado correctamente."));
    }

    // POST /api/admin/inventory/movements
    [HttpPost("movements")]
    public async Task<IActionResult> RegistrarMovimiento([FromBody] RegisterMovementRequestDto request)
    {
        var usuarioId = ObtenerUsuarioId();
        if (usuarioId == null) return Unauthorized();
        var movimiento = await _inventoryService.RegistrarMovimientoAsync(request, usuarioId.Value);
        return Ok(ApiResponseDto<InventoryMovementDto>.Ok(movimiento, "Movimiento registrado."));
    }

    // GET /api/admin/inventory/movements?inventoryItemId=xxx
    [HttpGet("movements")]
    public async Task<IActionResult> ListarMovimientos(
        [FromQuery] Guid? inventoryItemId,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var resultado = await _inventoryService.ListarMovimientosAsync(inventoryItemId, page, size);
        return Ok(ApiResponseDto<PagedResultDto<InventoryMovementDto>>.Ok(resultado));
    } 
    
    // GET /api/admin/inventory/{id}/history
    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> ObtenerHistorial(Guid id)
    {
        var historial = await _inventoryService.ObtenerHistorialAsync(id);
        return Ok(ApiResponseDto<InventoryHistoryDto>.Ok(historial));
    }

    // GET /api/admin/inventory/{id}/prediccion-surtido
    // Propuesta 1 (modelos predictivos): cantidad sugerida a surtir la próxima semana,
    // calculada por regresión lineal sobre el consumo semanal + módulo de temporada.
    [HttpGet("{id:guid}/prediccion-surtido")]
    public async Task<IActionResult> ObtenerPrediccionSurtido(Guid id)
    {
        var prediccion = await _inventoryService.ObtenerPrediccionSurtidoAsync(id);
        return Ok(ApiResponseDto<SupplyForecastDto>.Ok(prediccion));
    }

    // GET /api/admin/inventory/reabastecimiento
    // Propuesta 1 (modelos predictivos): lista de insumos a surtir, cada uno con el consumo
    // predicho por el modelo y la cantidad sugerida a comprar la próxima semana.
    [HttpGet("reabastecimiento")]
    public async Task<IActionResult> ObtenerReabastecimiento([FromQuery] bool refresh = false)
    {
        var lista = await _inventoryService.ObtenerReabastecimientoAsync(refresh);
        return Ok(ApiResponseDto<List<SupplyReplenishmentItemDto>>.Ok(lista));
    }

    // POST /api/admin/inventory/snapshots
    [HttpPost("snapshots")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> RegistrarSnapshot()
    {
        await _inventoryService.RegistrarSnapshotDiarioAsync();
        return Ok(ApiResponseDto<object>.Ok(new { }, "Snapshot diario registrado con éxito."));
    }

    private Guid? ObtenerUsuarioId()
    {
        var claim = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
