using FloreriaBautista.Data;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Services.Scheduler;

public class AuthTokenCleanupService : BackgroundService
{
    private static readonly TimeSpan Intervalo      = TimeSpan.FromHours(6);
    private static readonly TimeSpan RetencionUsado = TimeSpan.FromDays(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuthTokenCleanupService> _logger;

    public AuthTokenCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<AuthTokenCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
        catch (OperationCanceledException) { return; }

        _logger.LogInformation(
            "AuthTokenCleanupService iniciado. Limpieza cada {H}h.",
            Intervalo.TotalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            await LimpiarTokensAsync();

            try { await Task.Delay(Intervalo, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task LimpiarTokensAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var limite = DateTime.UtcNow - RetencionUsado;

            var eliminados = await db.AuthTokens
                .Where(t => t.Usado && t.CreadoEn < limite)
                .ExecuteDeleteAsync();

            if (eliminados > 0)
                _logger.LogInformation(
                    "AuthTokenCleanup: {N} tokens usados eliminados (> 30 días).",
                    eliminados);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en la limpieza de auth tokens.");
        }
    }
}
