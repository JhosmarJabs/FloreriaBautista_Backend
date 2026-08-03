using System.Net.Http.Json;
using FloreriaBautista.Models.DTOs.Analytics;
using FloreriaBautista.Services.Interfaces;

namespace FloreriaBautista.Services.Analytics;

// Habla con el sidecar de Python (ml-service) que carga modelo_surtido.pkl.
//
// Decisión de diseño: si el sidecar no responde, este cliente LANZA. No devuelve un
// número aproximado ni cae en una heurística silenciosa. El §13 del proyecto es
// explícito: "Si una función del sistema muestra resultados fijos, aleatorios o
// escritos directamente en el código, no se considerará una integración válida".
// Un error visible es preferible a una cifra que el usuario creería que salió del modelo.
public class MlPredictionClient : IMlPredictionClient
{
    private readonly HttpClient                 _http;
    private readonly ILogger<MlPredictionClient> _logger;

    public MlPredictionClient(HttpClient http, ILogger<MlPredictionClient> logger)
    {
        _http   = http;
        _logger = logger;
    }

    public async Task<SupplyPredictionResultDto> PredecirSurtidoAsync(
        SupplyFeaturesDto features, CancellationToken ct = default)
    {
        HttpResponseMessage respuesta;
        try
        {
            respuesta = await _http.PostAsJsonAsync("/predict/surtido", features, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "No se pudo contactar al servicio de inferencia en {BaseAddress}", _http.BaseAddress);
            throw new InvalidOperationException(
                "El servicio de predicción (ml-service) no está disponible. " +
                "Verifica que el contenedor esté levantado: docker compose up ml-service", ex);
        }

        if (!respuesta.IsSuccessStatusCode)
        {
            var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);
            _logger.LogError("ml-service respondió {Codigo}: {Cuerpo}", (int)respuesta.StatusCode, cuerpo);
            throw new InvalidOperationException(
                $"El servicio de predicción devolvió {(int)respuesta.StatusCode}. {cuerpo}");
        }

        var resultado = await respuesta.Content.ReadFromJsonAsync<SupplyPredictionResultDto>(cancellationToken: ct);
        return resultado ?? throw new InvalidOperationException(
            "El servicio de predicción devolvió una respuesta vacía.");
    }

    public async Task<MlServiceHealthDto> ObtenerEstadoAsync(CancellationToken ct = default)
    {
        try
        {
            return await _http.GetFromJsonAsync<MlServiceHealthDto>("/health", ct)
                   ?? new MlServiceHealthDto { Estado = "sin_respuesta" };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ml-service no respondió al health check");
            return new MlServiceHealthDto { Estado = "inaccesible", ErrorCarga = ex.Message };
        }
    }
}
