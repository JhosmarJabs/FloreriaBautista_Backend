using FloreriaBautista.Models.DTOs.Auth;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FloreriaBautista.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    // POST /api/auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        try
        {
            var result = await _authService.LoginAsync(request);
            return Ok(ApiResponseDto<LoginResponseDto>.Ok(result));
        }
        catch (UnauthorizedException ex)
        {
            return Unauthorized(ApiResponseDto<object>.Fail(ex.Message));
        }
    }

    // POST /api/auth/register
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
    {
        var result = await _authService.RegisterAsync(request);
        return Ok(ApiResponseDto<LoginResponseDto>.Ok(result));
    }

    // POST /api/auth/token/refresh
    [HttpPost("token/refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto request)
    {
        try
        {
            var result = await _authService.RefreshAsync(request.RefreshToken);
            return Ok(ApiResponseDto<LoginResponseDto>.Ok(result));
        }
        catch (UnauthorizedException ex)
        {
            return Unauthorized(ApiResponseDto<object>.Fail(ex.Message));
        }
    }

    // POST /api/auth/logout
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequestDto request)
    {
        await _authService.LogoutAsync(request.RefreshToken);
        return Ok(ApiResponseDto<object>.Ok(null, "Sesión cerrada correctamente."));
    }

    // POST /api/auth/logout/all
    [HttpPost("logout/all")]
    [Authorize]
    public async Task<IActionResult> LogoutAll()
    {
        var userId = ObtenerUsuarioId();
        if (userId == null)
            return Unauthorized(ApiResponseDto<object>.Fail("No autenticado."));

        await _authService.LogoutAllAsync(userId.Value);
        return Ok(ApiResponseDto<object>.Ok(null, "Todas las sesiones cerradas."));
    }

    private Guid? ObtenerUsuarioId()
    {
        var claim = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
