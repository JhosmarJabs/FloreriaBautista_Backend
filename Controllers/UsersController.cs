using FloreriaBautista.Models.DTOs.Auth;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FloreriaBautista.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IAuthService _authService;
    public UsersController(IAuthService authService) => _authService = authService;

    // GET /api/users/me
    [HttpGet("me")]
    public async Task<IActionResult> ObtenerPerfil()
    {
        var userId = ObtenerUsuarioId();
        if (userId == null)
            return Unauthorized(ApiResponseDto<object>.Fail("No autenticado."));

        var perfil = await _authService.ObtenerPerfilAsync(userId.Value);
        return Ok(ApiResponseDto<UserProfileDto>.Ok(perfil));
    }

    // POST /api/users/me
    [HttpPost("me")]
    public async Task<IActionResult> ActualizarPerfil([FromBody] UpdateProfileRequestDto request)
    {
        var userId = ObtenerUsuarioId();
        if (userId == null)
            return Unauthorized(ApiResponseDto<object>.Fail("No autenticado."));

        var perfil = await _authService.ActualizarPerfilAsync(userId.Value, request);
        return Ok(ApiResponseDto<UserProfileDto>.Ok(perfil, "Perfil actualizado correctamente."));
    }

    private Guid? ObtenerUsuarioId()
    {
        var claim = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
