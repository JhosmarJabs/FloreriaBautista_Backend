using FloreriaBautista.Models.DTOs.Auth;

namespace FloreriaBautista.Services.Interfaces;

public interface IAuthService
{
    Task<LoginResponseDto> LoginAsync(LoginRequestDto request);
    Task<LoginResponseDto> RefreshAsync(string refreshToken);
    Task LogoutAsync(string refreshToken);
    Task LogoutAllAsync(Guid userId);
    Task<LoginResponseDto> RegisterAsync(RegisterRequestDto request);
}
