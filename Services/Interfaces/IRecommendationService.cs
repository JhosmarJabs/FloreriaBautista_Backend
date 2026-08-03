using FloreriaBautista.Models.DTOs.Recommendations;

namespace FloreriaBautista.Services.Interfaces;

// Solución 2: recomendación por filtrado colaborativo ítem-ítem (similitud coseno,
// retroalimentación implícita). El backend NO entrena ni recalcula el modelo: carga el
// artefacto que produjo la libreta 02_recomendacion_colaborativa.ipynb y lo consulta.
public interface IRecommendationService
{
    // Relee recomendador.json desde disco, para que un artefacto regenerado entre en vigor
    // sin reiniciar el contenedor.
    Task<RecomendadorEstadoDto>           RecargarArtefactoAsync();

    // Estado del artefacto cargado, para diagnóstico desde el panel de administración.
    Task<RecomendadorEstadoDto>           ObtenerEstadoAsync();

    Task<List<ProductRecommendationDto>>  ObtenerRecomendadosAsync(IEnumerable<Guid> productosEnContexto, int topN = 4);
}
