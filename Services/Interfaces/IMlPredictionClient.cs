using FloreriaBautista.Models.DTOs.Analytics;

namespace FloreriaBautista.Services.Interfaces;

// Cliente del sidecar de inferencia (ml-service). El backend arma las variables desde la
// base de datos y delega la EJECUCIÓN del modelo al servicio que carga el .pkl generado
// por la libreta. Así no se reimplementa el algoritmo en C#, que es lo que exige el §5.6
// del proyecto: la predicción debe producirse ejecutando realmente el modelo entrenado.
public interface IMlPredictionClient
{
    Task<SupplyPredictionResultDto> PredecirSurtidoAsync(SupplyFeaturesDto features, CancellationToken ct = default);
    Task<MlServiceHealthDto>        ObtenerEstadoAsync(CancellationToken ct = default);
}
