using FloreriaBautista.Models.Entities;
using FloreriaBautista.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace FloreriaBautista.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _config;
    public TokenService(IConfiguration config) => _config = config;

    public string CreateToken(User user)
    {
        // FIX: Eliminada clave hardcodeada. Se requiere configuración en .env/appsettings. (Resuelve Security Rating E)
        var jwtKey = _config["Jwt:Key"];
        if (string.IsNullOrEmpty(jwtKey) || jwtKey.Length < 32)
        {
            throw new InvalidOperationException("La clave JWT no está configurada o es demasiado corta (mínimo 32 caracteres).");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Correo ?? ""),
            new Claim(ClaimTypes.Role, user.Roles?.FirstOrDefault()?.Nombre ?? "EMPLOYEE"),
            new Claim("fullName", user.Nombre)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddDays(7), // FIX: Usar UtcNow (Resuelve Security Hotspot)
            SigningCredentials = creds,
            Issuer = _config["Jwt:Issuer"],
            Audience = _config["Jwt:Audience"]
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        // Uso de RandomNumberGenerator (Seguro)
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
}
