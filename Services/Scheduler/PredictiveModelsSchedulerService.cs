using FloreriaBautista.Services.Interfaces;

namespace FloreriaBautista.Services.Scheduler;

// Recalcula periódicamente los modelos predictivos que dependen de datos agregados
// (Propuesta 2: reglas de asociación: Propuesta 3: segmentación RFM de clientes),
// para que no se queden desactualizados sin depender de que un admin lo dispare a mano.
public class PredictiveModelsSchedulerService : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory                        _scopeFactory;
    private readonly ILogger<PredictiveModelsSchedulerService>   _logger;

    public PredictiveModelsSchedulerService(IServiceScopeFactory scopeFactory, ILogger<PredictiveModelsSchedulerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RecalcularAsync();
            await Task.Delay(Intervalo, stoppingToken);
        }
    }

    private async Task RecalcularAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        try
        {
            var recomendaciones = scope.ServiceProvider.GetRequiredService<IRecommendationService>();
            var reglas = await recomendaciones.RecalcularReglasAsync();
            _logger.LogInformation("Reglas de asociación recalculadas automáticamente: {Reglas} reglas sobre {Ordenes} pedidos.",
                reglas.ReglasGeneradas, reglas.TransaccionesUsadas);
        }
        catch (Exception ex) { _logger.LogError(ex, "Error al recalcular reglas de asociación automáticamente"); }

        try
        {
            var segmentacion = scope.ServiceProvider.GetRequiredService<ICustomerSegmentationService>();
            var segmentos = await segmentacion.RecalcularSegmentosAsync();
            _logger.LogInformation("Segmentación de clientes recalculada automáticamente: {Clientes} clientes procesados.",
                segmentos.ClientesProcesados);
        }
        catch (Exception ex) { _logger.LogError(ex, "Error al recalcular segmentación de clientes automáticamente"); }
    }
}
