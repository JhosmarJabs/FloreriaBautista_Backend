using FloreriaBautista.Models.DTOs.Cms;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Services;
using FloreriaBautista.Services.Cms;
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
    private readonly ContenidoLegalService _legalService;
    public AdminCmsController(CmsService cmsService, ContenidoLegalService legalService)
    {
        _cmsService   = cmsService;
        _legalService = legalService;
    }

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

    // GET /api/admin/cms/legal — documentos con notas de revisión y estado.
    [HttpGet("legal")]
    public async Task<IActionResult> ObtenerLegal()
    {
        var docs = await _legalService.ObtenerAdminAsync();
        return Ok(ApiResponseDto<List<DocumentoLegalAdminDto>>.Ok(docs));
    }

    // PUT /api/admin/cms/legal/{slug} — solo los 5 slugs fijos; otro slug da 404.
    [HttpPut("legal/{slug}")]
    public async Task<IActionResult> ActualizarLegal(string slug, [FromBody] ActualizarDocumentoLegalDto request)
    {
        var doc = await _legalService.ActualizarAsync(slug, request, UsuarioId());
        return Ok(ApiResponseDto<DocumentoLegalAdminDto>.Ok(doc, "Página actualizada."));
    }

    private Guid? UsuarioId()
    {
        var claim = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
