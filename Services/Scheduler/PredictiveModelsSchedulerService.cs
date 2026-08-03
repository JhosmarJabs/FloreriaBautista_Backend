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
            // Solución 2: no se recalcula el modelo aquí. El artefacto lo produce la libreta;
            // el backend solo lo relee por si fue regenerado desde la última revisión.
            var recomendaciones = scope.ServiceProvider.GetRequiredService<IRecommendationService>();
            var estado = await recomendaciones.RecargarArtefactoAsync();
            if (estado.Disponible)
                _logger.LogInformation(
                    "Artefacto del recomendador recargado: {Productos} productos, configuración '{Config}', generado {Generado}.",
                    estado.NProductos, estado.Configuracion, estado.GeneradoEn);
            else
                _logger.LogWarning("El artefacto del recomendador no está disponible: {Error}", estado.Error);
        }
        catch (Exception ex) { _logger.LogError(ex, "Error al recargar el artefacto del recomendador"); }

        try
        {
            var segmentacion = scope.ServiceProvider.GetRequiredService<ICustomerSegmentationService>();
            var segmentos = await segmentacion.RecalcularSegmentosAsync();
            _logger.LogInformation("Segmentación de clientes recalculada automáticamente: {Clientes} clientes procesados.",
                segmentos.ClientesProcesados);
        }
        catch (Exception ex) { _logger.LogError(ex, "Error al recalcular segmentación de clientes automáticamente"); }

        try
        {
            // Solución 1: precalienta la lista de reabastecimiento (ejecuta el modelo por insumo)
            // para que la pantalla de admin la lea instantáneamente en vez de calcularla al abrir.
            var inventario = scope.ServiceProvider.GetRequiredService<IInventoryService>();
            var reab = await inventario.ObtenerReabastecimientoAsync(refresh: true);
            _logger.LogInformation("Reabastecimiento precalculado y cacheado: {Insumos} insumos.", reab.Count);
        }
        catch (Exception ex) { _logger.LogError(ex, "Error al precalcular el reabastecimiento"); }
    }
}
