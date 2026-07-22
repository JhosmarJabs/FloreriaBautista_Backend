using FloreriaBautista.Models.DTOs.Analytics;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Recommendations;
using FloreriaBautista.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FloreriaBautista.Controllers.Admin;

// Endpoints administrativos de los modelos predictivos (Propuestas 2 y 3): recalcular reglas
// de asociación y segmentación de clientes bajo demanda, y consultar sus resultados.
[ApiController]
[Tags("4. Modelos Predictivos")]
[Route("api/admin/analytics")]
[Authorize(Roles = "ADMIN")]
public class AdminAnalyticsController : ControllerBase
{
    private readonly IRecommendationService        _recommendationService;
    private readonly ICustomerSegmentationService  _segmentationService;

    public AdminAnalyticsController(IRecommendationService recommendationService, ICustomerSegmentationService segmentationService)
    {
        _recommendationService = recommendationService;
        _segmentationService   = segmentationService;
    }

    // POST /api/admin/analytics/reglas-asociacion/recalcular
    [HttpPost("reglas-asociacion/recalcular")]
    public async Task<IActionResult> RecalcularReglasAsociacion()
    {
        var resultado = await _recommendationService.RecalcularReglasAsync();
        return Ok(ApiResponseDto<RecalcularReglasResultDto>.Ok(resultado, "Reglas de asociación recalculadas."));
    }

    // GET /api/admin/analytics/segmentos-clientes
    [HttpGet("segmentos-clientes")]
    public async Task<IActionResult> ObtenerSegmentosClientes()
    {
        var segmentos = await _segmentationService.ObtenerSegmentosAsync();
        return Ok(ApiResponseDto<List<CustomerSegmentGroupDto>>.Ok(segmentos));
    }

    // POST /api/admin/analytics/segmentos-clientes/recalcular
    [HttpPost("segmentos-clientes/recalcular")]
    public async Task<IActionResult> RecalcularSegmentosClientes()
    {
        var resultado = await _segmentationService.RecalcularSegmentosAsync();
        return Ok(ApiResponseDto<RecalcularSegmentosResultDto>.Ok(resultado, "Segmentación de clientes recalculada."));
    }
}
