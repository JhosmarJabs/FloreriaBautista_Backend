using Microsoft.AspNetCore.Mvc;

namespace FloreriaBautista.Controllers;

[ApiController]
[Tags("3. Sistema y Seguridad")]
public class PingController : ControllerBase
{
    // GET /api/ping
    // Endpoint publico y sin dependencias (no toca BD) pensado para keep-alive externo (Render free tier).
    [HttpGet("api/ping")]
    public IActionResult Ping() => Ok(new { status = "awake", timestamp = DateTime.UtcNow });
}
