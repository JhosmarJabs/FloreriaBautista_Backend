using FloreriaBautista.Services.Interfaces;

namespace FloreriaBautista.Services.Scheduler;

/// <summary>
/// Dispara la regla de expiracion (<see cref="InstantSaleExpirer"/>) cada 30
/// segundos, y una primera vez en cuanto arranca el backend. Replica exacta
/// del patron <see cref="OrderArchiverService"/>: scope-por-pasada, reintento
/// rapido si falla, cancelacion limpia al detener.
/// </summary>
public class InstantSaleExpirerService : BackgroundService
{
    private static readonly TimeSpan IntervaloRevision  = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan IntervaloReintento = TimeSpan.FromSeconds(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InstantSaleExpirerService> _logger;

    public InstantSaleExpirerService(
        IServiceScopeFactory scopeFactory,
        ILogger<InstantSaleExpirerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "InstantSaleExpirerService iniciado. Revision cada {Seg}s (la primera, ahora).",
            IntervaloRevision.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            var ok = await EjecutarPasadaAsync();

            try
            {
                await Task.Delay(ok ? IntervaloRevision : IntervaloReintento, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("InstantSaleExpirerService detenido.");
    }

    private async Task<bool> EjecutarPasadaAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var expirer     = scope.ServiceProvider.GetRequiredService<IInstantSaleExpirer>();
            await expirer.RevisarPendientesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error al revisar solicitudes de venta instantanea; se reintentara en {Seg}s.",
                IntervaloReintento.TotalSeconds);
            return false;
        }
    }
}
