using FloreriaBautista.Services.Interfaces;

namespace FloreriaBautista.Services.Scheduler;

/// <summary>
/// Dispara la regla de archivo (<see cref="OrderArchiver"/>) cada hora, y una
/// primera vez en cuanto arranca el backend — la pasada inicial ocurre ANTES del
/// primer <c>Task.Delay</c>, así que un reinicio siempre deja la vista limpia sin
/// esperar la siguiente ventana.
///
/// RED DE SEGURIDAD DOBLE — no borrar ninguna de las dos mitades:
/// esta ventana de 1 hora significa que un pedido atrasado puede seguir en la
/// tabla hasta 60 minutos después de vencer. Por eso
/// <c>OrderService.ListarAdminAsync</c> filtra además por
/// <c>FechaEntrega &gt;= hoy</c> en la vista activa. Ese filtro NO es redundante:
/// es lo que hace que el usuario nunca vea un pedido vencido, aunque el
/// archivador todavía no haya corrido. Y este servicio tampoco es redundante: es
/// lo que persiste <c>Archivado</c> y el estado NO_COMPLETADO.
/// </summary>
public class OrderArchiverService : BackgroundService
{
    private static readonly TimeSpan IntervaloRevision = TimeSpan.FromHours(1);
    private static readonly TimeSpan IntervaloReintento = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderArchiverService> _logger;

    public OrderArchiverService(IServiceScopeFactory scopeFactory, ILogger<OrderArchiverService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrderArchiverService iniciado. Revisión cada {Horas}h (la primera, ahora).",
            IntervaloRevision.TotalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            var ok = await EjecutarPasadaAsync();

            try
            {
                // Si la pasada falló (p. ej. la base todavía no acepta conexiones
                // recién arrancado el contenedor) se reintenta pronto, no en 1 hora.
                await Task.Delay(ok ? IntervaloRevision : IntervaloReintento, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("OrderArchiverService detenido.");
    }

    private async Task<bool> EjecutarPasadaAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var archiver    = scope.ServiceProvider.GetRequiredService<IOrderArchiver>();
            await archiver.ArchivarAtrasadosAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al archivar pedidos atrasados; se reintentará en {Minutos} min.",
                IntervaloReintento.TotalMinutes);
            return false;
        }
    }
}
