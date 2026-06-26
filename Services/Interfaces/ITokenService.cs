using FloreriaBautista.Models.Entities;

namespace FloreriaBautista.Services.Interfaces;

public interface ITokenService
{
    string CreateToken(User user);
    string GenerateRefreshToken();
}
