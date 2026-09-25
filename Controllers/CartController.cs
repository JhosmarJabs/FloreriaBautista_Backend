using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Promotions;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FloreriaBautista.Controllers;

[ApiController]
[Tags("Privado o Cliente")]
[Route("api/cart")]
[Authorize]
public class CartController : ControllerBase
{
    private readonly IPricingService _pricing;
    public CartController(IPricingService pricing) => _pricing = pricing;

    [HttpPost("aplicar-cupon")]
    public async Task<IActionResult> AplicarCupon([FromBody] AplicarCuponRequestDto request)
    {
        if (request.Items.Count == 0)
            return BadRequest(ApiResponseDto<object>.Fail("El carrito está vacío."));

        if (string.IsNullOrWhiteSpace(request.CodigoCupon))
            return BadRequest(ApiResponseDto<object>.Fail("Debe ingresar un código de cupón."));

        try
        {
            var breakdown = await _pricing.CalcularTotalAsync(request.Items, request.CodigoCupon);
            return Ok(ApiResponseDto<PricingBreakdownDto>.Ok(breakdown));
        }
        catch (AppException ex)
        {
            return BadRequest(ApiResponseDto<object>.Fail(ex.Message));
        }
    }

    [HttpPost("calcular")]
    public async Task<IActionResult> Calcular([FromBody] AplicarCuponRequestDto request)
    {
        if (request.Items.Count == 0)
            return BadRequest(ApiResponseDto<object>.Fail("El carrito está vacío."));

        try
        {
            var codigoCupon = string.IsNullOrWhiteSpace(request.CodigoCupon) ? null : request.CodigoCupon;
            var breakdown = await _pricing.CalcularTotalAsync(request.Items, codigoCupon);
            return Ok(ApiResponseDto<PricingBreakdownDto>.Ok(breakdown));
        }
        catch (AppException ex)
        {
            return BadRequest(ApiResponseDto<object>.Fail(ex.Message));
        }
    }
}
