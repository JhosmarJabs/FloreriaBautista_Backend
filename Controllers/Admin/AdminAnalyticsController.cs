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

    // GET /api/admin/analytics/recomendador
    // Estado del artefacto cargado: qué configuración, qué versión de scikit-learn y
    // cuándo lo generó la libreta. Sirve para comprobar en vivo que el sistema está
    // usando el mismo artefacto que se reporta en el PDF.
    [HttpGet("recomendador")]
    public async Task<IActionResult> ObtenerEstadoRecomendador()
    {
        var estado = await _recommendationService.ObtenerEstadoAsync();
        return Ok(ApiResponseDto<RecomendadorEstadoDto>.Ok(estado));
    }

    // POST /api/admin/analytics/recomendador/recargar
    // Relee recomendador.json desde disco. Es el procedimiento de actualización del modelo
    // cuando la libreta genera un artefacto nuevo, sin reiniciar el contenedor.
    [HttpPost("recomendador/recargar")]
    public async Task<IActionResult> RecargarRecomendador()
    {
        var estado = await _recommendationService.RecargarArtefactoAsync();
        return estado.Disponible
            ? Ok(ApiResponseDto<RecomendadorEstadoDto>.Ok(estado, "Artefacto del recomendador recargado."))
            : StatusCode(503, ApiResponseDto<RecomendadorEstadoDto>.Ok(estado,
                  $"No se pudo cargar el artefacto: {estado.Error}"));
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
