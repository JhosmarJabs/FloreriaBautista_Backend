using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Inventory;
using FloreriaBautista.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FloreriaBautista.Controllers;

[ApiController]
[Tags("Administrador")]
[Route("api/admin/inventory")]
[Authorize(Roles = "ADMIN,EMPLEADO")]
public class AdminInventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;
    public AdminInventoryController(IInventoryService inventoryService)
        => _inventoryService = inventoryService;

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

    // POST /api/admin/inventory/{id}
    [HttpPost("{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] UpdateInventoryItemDto request)
    {
        var item = await _inventoryService.ActualizarAsync(id, request);
        return Ok(ApiResponseDto<InventoryItemDto>.Ok(item, "Insumo actualizado correctamente."));
    }

    // POST /api/admin/inventory/{id}/delete
    [HttpPost("{id:guid}/delete")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Eliminar(Guid id)
    {
        await _inventoryService.EliminarAsync(id);
        return Ok(ApiResponseDto<object>.Ok(null!, "Insumo desactivado correctamente."));
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

    // GET /api/admin/inventory/alerts
    [HttpGet("alerts")]
    public async Task<IActionResult> ListarAlertas([FromQuery] string? sucursal, [FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        var resultado = await _inventoryService.ListarAsync(sucursal, true, null, page, size);
        return Ok(ApiResponseDto<PagedResultDto<InventoryItemDto>>.Ok(resultado));
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

    private Guid? ObtenerUsuarioId()
    {
        var claim = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
