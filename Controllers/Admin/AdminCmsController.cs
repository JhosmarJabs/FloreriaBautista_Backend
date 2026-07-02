using FloreriaBautista.Models.DTOs.Cms;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FloreriaBautista.Controllers.Admin;

[ApiController]
[Tags("3. Sistema y Seguridad")]
[Route("api/admin/cms")]
[Authorize(Roles = "ADMIN")]
public class AdminCmsController : ControllerBase
{
    private readonly CmsService _cmsService;
    public AdminCmsController(CmsService cmsService) => _cmsService = cmsService;

    // GET /api/admin/cms
    [HttpGet]
    public async Task<IActionResult> Obtener()
    {
        var settings = await _cmsService.ObtenerAsync();
        return Ok(ApiResponseDto<SiteSettingsDto>.Ok(settings));
    }

    // POST /api/admin/cms
    [HttpPost]
    public async Task<IActionResult> Actualizar([FromBody] SiteSettingsDto request)
    {
        var settings = await _cmsService.ActualizarAsync(request);
        return Ok(ApiResponseDto<SiteSettingsDto>.Ok(settings, "Configuración del CMS actualizada."));
    }
}
