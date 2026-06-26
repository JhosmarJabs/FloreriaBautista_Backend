using FloreriaBautista.Models.DTOs.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FloreriaBautista.Controllers.Admin;

[ApiController]
[Tags("3. Sistema y Seguridad")]
[Route("api/admin/cms")]
[Authorize(Roles = "ADMIN")]
public class AdminCmsController : ControllerBase
{
    [HttpPost]
    public IActionResult ActualizarCms([FromBody] object request)
    {
        // TODO: Mapear este endpoint cuando se cree la tabla de configuración real del CMS
        return Ok(ApiResponseDto<object>.Ok(request, "Configuración del CMS actualizada."));
    }
}
