using FloreriaBautista.Models.DTOs.Cms;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Services;
using FloreriaBautista.Services.Cms;
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
    private readonly ContenidoLegalService _legalService;
    public CmsController(CmsService cmsService, ContenidoLegalService legalService)
    {
        _cmsService   = cmsService;
        _legalService = legalService;
    }

    // GET /api/cms
    [HttpGet]
    public async Task<IActionResult> Obtener()
    {
        var settings = await _cmsService.ObtenerAsync();
        return Ok(ApiResponseDto<SiteSettingsDto>.Ok(settings));
    }

    // GET /api/cms/legal — los 5 documentos legales y de ayuda (sin notas de revisión).
    [HttpGet("legal")]
    public async Task<IActionResult> ObtenerLegal()
    {
        var legal = await _legalService.ObtenerPublicoAsync();
        return Ok(ApiResponseDto<ContenidoLegalPublicoDto>.Ok(legal));
    }
}
