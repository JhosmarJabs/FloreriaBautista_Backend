using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace FloreriaBautista.Controllers;

[ApiController]
[Tags("Desarrollo")]
[Route("api/dev")]
public class DevController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    public DevController(IWebHostEnvironment env) => _env = env;

    // GET /api/dev/token
    [HttpGet("token")]
    public IActionResult GenerarTokenAdmin()
    {
        if (!_env.IsDevelopment()) return NotFound();

        var key      = Environment.GetEnvironmentVariable("JWT_KEY");
        var issuer   = Environment.GetEnvironmentVariable("JWT_ISSUER");
        var audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE");

        if (string.IsNullOrEmpty(key) || key.Length < 32)
            return BadRequest(new { error = $"JWT_KEY muy corta ({key?.Length ?? 0} chars, necesita >= 32)" });
        if (string.IsNullOrEmpty(issuer))
            return BadRequest(new { error = "JWT_ISSUER no configurado en .env" });
        if (string.IsNullOrEmpty(audience))
            return BadRequest(new { error = "JWT_AUDIENCE no configurado en .env" });

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   "00000001-0000-0000-0000-000000000001"), 
            new Claim(JwtRegisteredClaimNames.Email, "admin@floreriabautista.com"),
            new Claim(ClaimTypes.NameIdentifier,     "00000001-0000-0000-0000-000000000001"),
            new Claim(ClaimTypes.Role,               "ADMIN"),
        };

        var tokenObj = new JwtSecurityToken(
            issuer:             issuer,
            audience:           audience,
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            DateTime.UtcNow.AddHours(24),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256)
        );

        var tokenStr = new JwtSecurityTokenHandler().WriteToken(tokenObj);

        return Ok(tokenStr);
    }
}
