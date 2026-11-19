using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Inventory;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace FloreriaBautista.Controllers.Admin;

[ApiController]
[Tags("2. Operaciones y Ventas")]
[Route("api/admin/availability")]
[Authorize(Roles = "ADMIN,EMPLEADO")]
public class AdminAvailabilityController : ControllerBase
{
    private readonly IAvailabilityService _availability;
    private readonly IFechaHelper _fechas;

    public AdminAvailabilityController(IAvailabilityService availability, IFechaHelper fechas)
    {
        _availability = availability;
        _fechas       = fechas;
    }

    [HttpGet("supply/{inventoryItemId:guid}")]
    public async Task<IActionResult> DisponibilidadInsumo(Guid inventoryItemId)
    {
        var result = await _availability.ObtenerDisponibilidadInsumoAsync(inventoryItemId);
        return Ok(ApiResponseDto<SupplyAvailabilityDto>.Ok(result));
    }

    [HttpGet("product/{productId:guid}")]
    public async Task<IActionResult> DisponibilidadProducto(Guid productId)
    {
        var result = await _availability.ObtenerDisponibilidadProductoAsync(productId);
        return Ok(ApiResponseDto<ProductAvailabilityDto>.Ok(result));
    }

    [HttpPost("validate-instant")]
    public async Task<IActionResult> ValidarInstantanea([FromBody] ValidateInstantRequest request)
    {
        var result = await _availability.ValidarVentaInstantaneaAsync(request.ProductId, request.Cantidad);
        return Ok(ApiResponseDto<SaleValidationResult>.Ok(result));
    }

    [HttpPost("validate-advance")]
    public async Task<IActionResult> ValidarAnticipada([FromBody] ValidateAdvanceRequest request)
    {
        var fechaEntrega = request.FechaEntrega!.Value;
        if (fechaEntrega < _fechas.HoyLocal())
            throw new AppException("La fecha de entrega no puede ser anterior a hoy.");

        var result = await _availability.ValidarVentaAnticipadaAsync(
            request.ProductId, request.Cantidad, fechaEntrega);
        return Ok(ApiResponseDto<AdvanceSaleValidationResult>.Ok(result));
    }

    [HttpGet("supply-cycle")]
    public async Task<IActionResult> CicloAbastecimiento()
    {
        var result = await _availability.ObtenerCicloAbastecimientoAsync();
        return Ok(ApiResponseDto<SupplyCycleDto>.Ok(result));
    }
}

public class ValidateInstantRequest
{
    public Guid ProductId { get; set; }
    [Range(1, 999)]
    public int  Cantidad  { get; set; }
}

public class ValidateAdvanceRequest
{
    public Guid    ProductId    { get; set; }
    [Range(1, 999)]
    public int     Cantidad     { get; set; }
    [Required]
    public DateOnly? FechaEntrega { get; set; }
}
