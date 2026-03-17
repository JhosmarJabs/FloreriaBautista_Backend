using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Inventory;
using FloreriaBautista.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FloreriaBautista.Controllers;

[ApiController]
[Route("api/admin/inventory")]
[Authorize(Roles = "ADMIN,INVENTARIO")]
public class AdminInventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;
    public AdminInventoryController(IInventoryService inventoryService)
        => _inventoryService = inventoryService;

    // GET /api/admin/inventory?sucursal=PRINCIPAL&bajoMinimo=true
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string? sucursal,
        [FromQuery] bool?   bajoMinimo,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var resultado = await _inventoryService.ListarAsync(sucursal, bajoMinimo, page, size);
        return Ok(ApiResponseDto<PagedResultDto<InventoryItemDto>>.Ok(resultado));
    }

    // GET /api/admin/inventory/{productId}
    [HttpGet("{productId:guid}")]
    public async Task<IActionResult> Detalle(Guid productId)
    {
        var item = await _inventoryService.ObtenerPorProductoAsync(productId);
        return Ok(ApiResponseDto<InventoryItemDto>.Ok(item));
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

    // GET /api/admin/inventory/movements?productId=xxx
    [HttpGet("movements")]
    public async Task<IActionResult> ListarMovimientos(
        [FromQuery] Guid? productId,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var resultado = await _inventoryService.ListarMovimientosAsync(productId, page, size);
        return Ok(ApiResponseDto<PagedResultDto<InventoryMovementDto>>.Ok(resultado));
    }

    private Guid? ObtenerUsuarioId()
    {
        var claim = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
