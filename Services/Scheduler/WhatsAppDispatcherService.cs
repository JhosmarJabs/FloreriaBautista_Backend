using FloreriaBautista.Data;
using FloreriaBautista.Services.WhatsApp;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Services.Scheduler;

/// <summary>
/// Envía la cola de WhatsApp cada 10 s, solo con WhatsApp CONECTADO: hasta 10 mensajes por pasada,
/// 3 s entre envíos, expiración a los 120 min y 3 intentos. Una vez al día borra lo de más de 30 días.
/// Supone una sola instancia del backend (Render, un proceso). Con varias haría falta un bloqueo
/// (<c>FOR UPDATE SKIP LOCKED</c>).
/// </summary>
public class WhatsAppDispatcherService : BackgroundService
{
    private static readonly TimeSpan Intervalo       = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan EntreMensajes   = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan Retencion       = TimeSpan.FromDays(30);
    private const int TamanoLote = 10;

    private readonly IServiceScopeFactory               _scopeFactory;
    private readonly WhatsAppConfig                     _config;
    private readonly WhatsAppEstadoActual               _estadoActual;
    private readonly ILogger<WhatsAppDispatcherService> _logger;
    private DateTime _ultimaLimpieza = DateTime.MinValue;

    public WhatsAppDispatcherService(IServiceScopeFactory scopeFactory, WhatsAppConfig config,
        WhatsAppEstadoActual estadoActual, ILogger<WhatsAppDispatcherService> logger)
    {
        _scopeFactory = scopeFactory;
        _config       = config;
        _estadoActual = estadoActual;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.Configurado)
        {
            _logger.LogInformation("WhatsAppDispatcherService: integración no configurada, no se inicia.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EjecutarPasadaAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en la pasada del despachador de WhatsApp");
            }

            try { await Task.Delay(Intervalo, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task EjecutarPasadaAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (DateTime.UtcNow - _ultimaLimpieza > TimeSpan.FromDays(1))
        {
            var limite = DateTime.UtcNow - Retencion;
            var borrados = await db.WhatsAppMessages.Where(m => m.CreadoEn < limite).ExecuteDeleteAsync(ct);
            _ultimaLimpieza = DateTime.UtcNow;
            if (borrados > 0) _logger.LogInformation("WhatsApp: {N} mensajes de más de 30 días borrados", borrados);
        }

        // Estado en memoria (lo escribe el monitor). Al arrancar, antes de la primera pasada del
        // monitor, se lee una vez de la BD.
        var estado = _estadoActual.Estado;
        if (estado is null)
        {
            estado = await db.WhatsAppSettings.AsNoTracking()
                .Where(s => s.Id == 1).Select(s => s.EstadoConexion).FirstOrDefaultAsync(ct);
            _estadoActual.Estado = estado;
        }
        if (estado != WhatsAppConstantes.Conectado) return;

        var lote = await db.WhatsAppMessages
            .Include(m => m.Recipient)
            .Where(m => m.Estado == "PENDIENTE")
            .OrderBy(m => m.CreadoEn)
            .Take(TamanoLote)
            .ToListAsync(ct);
        if (lote.Count == 0) return;

        var cliente = scope.ServiceProvider.GetRequiredService<IEvolutionClient>();
        var enviadoAlguno = false;

        foreach (var m in lote)
        {
            if (DateTime.UtcNow - m.CreadoEn > WhatsAppConfig.ExpiracionMensaje)
            {
                m.Estado = "EXPIRADO";
                await db.SaveChangesAsync(ct);
                continue;
            }

            if (_estadoActual.Estado != WhatsAppConstantes.Conectado) break;
            if (enviadoAlguno) await Task.Delay(EntreMensajes, ct);

            var r = await cliente.EnviarTextoAsync(m.Recipient.Telefono, m.Texto, ct);
            enviadoAlguno = true;

            // Evolution caído no es culpa del mensaje: no gasta intentos, espera al monitor.
            if (r.Inalcanzable) break;

            if (r.Exito)
            {
                m.Estado    = "ENVIADO";
                m.EnviadoEn = DateTime.UtcNow;
            }
            else
            {
                m.Intentos++;
                var error = r.Mensaje ?? "Error desconocido";
                m.UltimoError = error.Length > 500 ? error[..500] : error;
                if (m.Intentos >= WhatsAppConfig.MaxIntentos) m.Estado = "FALLIDO";
            }
            await db.SaveChangesAsync(ct);
        }
    }
}
