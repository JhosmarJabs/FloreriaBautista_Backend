using FloreriaBautista.Models.DTOs.Recommendations;

namespace FloreriaBautista.Services.Interfaces;

public interface IRecommendationService
{
    // Propuesta 2: recomendación por reglas de asociación (market basket / Apriori de pares).
    Task<RecalcularReglasResultDto>       RecalcularReglasAsync();
    Task<List<ProductRecommendationDto>>  ObtenerRecomendadosAsync(IEnumerable<Guid> productosEnContexto, int topN = 4);
}
