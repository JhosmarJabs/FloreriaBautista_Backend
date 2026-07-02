using FloreriaBautista.Models.DTOs.Cms;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Services;
using Microsoft.AspNetCore.Mvc;

namespace FloreriaBautista.Controllers;

// Endpoint público: lo consumen las páginas del sitio (footer, contacto, banner del home)
// para mostrar exactamente el mismo contenido que se configura en /admin/cms.
[ApiController]
[Tags("Público")]
[Route("api/cms")]
public class CmsController : ControllerBase
{
    private readonly CmsService _cmsService;
    public CmsController(CmsService cmsService) => _cmsService = cmsService;

    // GET /api/cms
    [HttpGet]
    public async Task<IActionResult> Obtener()
    {
        var settings = await _cmsService.ObtenerAsync();
        return Ok(ApiResponseDto<SiteSettingsDto>.Ok(settings));
    }
}
