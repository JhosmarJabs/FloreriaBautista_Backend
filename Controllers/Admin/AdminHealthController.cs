using FloreriaBautista.Models.DTOs.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FloreriaBautista.Controllers.Admin;

[ApiController]
[Tags("3. Sistema y Seguridad")]
[Authorize(Roles = "ADMIN")]
public class AdminHealthController : ControllerBase
{
    // GET /api/admin/health
    [HttpGet("api/admin/health")]
    public IActionResult Health()
    {
        var data = new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = "1.0.0",
            Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64).ToString(@"dd\.hh\:mm\:ss")
        };
        return Ok(ApiResponseDto<object>.Ok(data));
    }
}
