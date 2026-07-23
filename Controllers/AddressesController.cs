using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Users;
using FloreriaBautista.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FloreriaBautista.Controllers;

[ApiController]
[Tags("Privado o Cliente")]
[Route("api/users/me/addresses")]
[Authorize]
public class AddressesController : ControllerBase
{
    private readonly IAddressService _addressService;
    public AddressesController(IAddressService addressService) => _addressService = addressService;

    // GET /api/users/me/addresses
    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var userId = ObtenerUsuarioId();
        if (userId == null)
            return Unauthorized(ApiResponseDto<object>.Fail("No autenticado."));

        var direcciones = await _addressService.ListarMisDireccionesAsync(userId.Value);
        return Ok(ApiResponseDto<List<AddressDto>>.Ok(direcciones));
    }

    // POST /api/users/me/addresses
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CreateAddressRequestDto request)
    {
        var userId = ObtenerUsuarioId();
        if (userId == null)
            return Unauthorized(ApiResponseDto<object>.Fail("No autenticado."));

        var creada = await _addressService.CrearAsync(userId.Value, request);
        return Ok(ApiResponseDto<AddressDto>.Ok(creada, "Dirección agregada correctamente."));
    }

    // PUT /api/users/me/addresses/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] UpdateAddressRequestDto request)
    {
        var userId = ObtenerUsuarioId();
        if (userId == null)
            return Unauthorized(ApiResponseDto<object>.Fail("No autenticado."));

        var actualizada = await _addressService.ActualizarAsync(userId.Value, id, request);
        return Ok(ApiResponseDto<AddressDto>.Ok(actualizada, "Dirección actualizada correctamente."));
    }

    // PATCH /api/users/me/addresses/{id}/principal
    [HttpPatch("{id:guid}/principal")]
    public async Task<IActionResult> MarcarPrincipal(Guid id)
    {
        var userId = ObtenerUsuarioId();
        if (userId == null)
            return Unauthorized(ApiResponseDto<object>.Fail("No autenticado."));

        var principal = await _addressService.MarcarPrincipalAsync(userId.Value, id);
        return Ok(ApiResponseDto<AddressDto>.Ok(principal, "Dirección principal actualizada."));
    }

    // DELETE /api/users/me/addresses/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Eliminar(Guid id)
    {
        var userId = ObtenerUsuarioId();
        if (userId == null)
            return Unauthorized(ApiResponseDto<object>.Fail("No autenticado."));

        await _addressService.EliminarAsync(userId.Value, id);
        return Ok(ApiResponseDto<object>.Ok(null!, "Dirección eliminada correctamente."));
    }

    private Guid? ObtenerUsuarioId()
    {
        var claim = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
