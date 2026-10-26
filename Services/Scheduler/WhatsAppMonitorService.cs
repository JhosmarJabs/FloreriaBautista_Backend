using FloreriaBautista.Services.WhatsApp;

namespace FloreriaBautista.Services.Scheduler;

/// <summary>
/// Consulta el estado de la conexión con Evolution cada 60 s (la primera, al arrancar) y lo registra
/// con <see cref="WhatsAppConnectionMonitor"/>. Es el respaldo del webhook <c>CONNECTION_UPDATE</c>.
/// No hace nada si la integración no está configurada.
/// </summary>
public class WhatsAppMonitorService : BackgroundService
{
    private readonly IServiceScopeFactory            _scopeFactory;
    private readonly WhatsAppConfig                  _config;
    private readonly ILogger<WhatsAppMonitorService> _logger;

    public WhatsAppMonitorService(IServiceScopeFactory scopeFactory, WhatsAppConfig config,
        ILogger<WhatsAppMonitorService> logger)
    {
        _scopeFactory = scopeFactory;
        _config       = config;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.Configurado)
        {
            _logger.LogInformation("WhatsAppMonitorService: integración no configurada, no se inicia.");
            return;
        }

        _logger.LogInformation("WhatsAppMonitorService iniciado. Revisión cada {Seg}s.",
            WhatsAppConfig.IntervaloMonitoreo.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            await EjecutarPasadaAsync(stoppingToken);
            try { await Task.Delay(WhatsAppConfig.IntervaloMonitoreo, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task EjecutarPasadaAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var cliente = scope.ServiceProvider.GetRequiredService<IEvolutionClient>();
            var monitor = scope.ServiceProvider.GetRequiredService<WhatsAppConnectionMonitor>();

            var r = await cliente.ObtenerEstadoAsync(ct);
            string estado;
            if (r.Exito) estado = WhatsAppConnectionMonitor.Traducir(r.Valor);
            else if (r.CodigoError == EvolutionCodigos.NoEncontrado) estado = WhatsAppConstantes.Desconectado;
            else estado = WhatsAppConstantes.ServicioNoDisponible;

            string? numero = null;
            if (estado == WhatsAppConstantes.Conectado)
            {
                var inst = await cliente.ObtenerInstanciaAsync(ct);
                if (inst.Exito) numero = inst.Valor?.Numero;
            }

            await monitor.RegistrarEstadoAsync(estado, "monitor", numero);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en la pasada del monitor de WhatsApp");
        }
    }
}
