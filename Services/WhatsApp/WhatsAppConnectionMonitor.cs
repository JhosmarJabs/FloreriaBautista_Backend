using FloreriaBautista.Data;
using FloreriaBautista.Hubs;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Services.Notifications;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Services.WhatsApp;

/// <summary>
/// Lógica compartida de cambio de estado de la conexión (la usan el webhook y el monitor periódico).
/// El aviso de desconexión es solo para ADMIN (notificación persistida + SignalR) y nunca sale por WhatsApp.
/// </summary>
public class WhatsAppConnectionMonitor
{
    private readonly AppDbContext                        _db;
    private readonly INotificationService                _notificaciones;
    private readonly IHubContext<VentaInstantaneaHub>    _hub;
    private readonly WhatsAppEstadoActual                _estadoActual;
    private readonly ILogger<WhatsAppConnectionMonitor>  _logger;

    public WhatsAppConnectionMonitor(AppDbContext db, INotificationService notificaciones,
        IHubContext<VentaInstantaneaHub> hub, WhatsAppEstadoActual estadoActual,
        ILogger<WhatsAppConnectionMonitor> logger)
    {
        _db             = db;
        _notificaciones = notificaciones;
        _hub            = hub;
        _estadoActual   = estadoActual;
        _logger         = logger;
    }

    /// <summary>Traduce el estado de Evolution (<c>open | connecting | close</c>) al estado interno.</summary>
    public static string Traducir(string? estadoEvolution) => estadoEvolution?.ToLowerInvariant() switch
    {
        "open"       => WhatsAppConstantes.Conectado,
        "connecting" => WhatsAppConstantes.Conectando,
        _            => WhatsAppConstantes.Desconectado
    };

    public async Task RegistrarEstadoAsync(string estado, string origen, string? numeroVinculado = null)
    {
        var s = await _db.WhatsAppSettings.FirstOrDefaultAsync(x => x.Id == 1);
        if (s is null)
        {
            s = new WhatsAppSettings { Id = 1 };
            _db.WhatsAppSettings.Add(s);
        }

        var ahora     = DateTime.UtcNow;
        var anterior  = s.EstadoConexion;
        var emitir    = false;

        // Gracia de arranque (plan gratuito): recién arrancado el backend, Evolution puede seguir dormido.
        // Un servicio no disponible en ese lapso se registra como CONECTANDO y no cuenta como caída.
        var enGraciaArranque = estado == WhatsAppConstantes.ServicioNoDisponible
            && ahora - _estadoActual.ArrancoEn < WhatsAppConfig.GraciaArranque;
        if (enGraciaArranque)
        {
            estado = WhatsAppConstantes.Conectando;
            if (anterior == WhatsAppConstantes.Conectado) _estadoActual.VeniaConectadoAntesDeGracia = true;
        }
        // Caída tras el enmascarado de la gracia (U-B3a): un `close` cuenta en cualquier momento;
        // cualquier otro estado distinto de CONECTADO cuenta cuando la gracia ya venció.
        var graciaVencida = ahora - _estadoActual.ArrancoEn >= WhatsAppConfig.GraciaArranque;
        var veniaConectado = anterior == WhatsAppConstantes.Conectado
            || (_estadoActual.VeniaConectadoAntesDeGracia && anterior == WhatsAppConstantes.Conectando
                && !enGraciaArranque
                && (estado == WhatsAppConstantes.Desconectado
                    || (estado != WhatsAppConstantes.Conectado && graciaVencida)));
        if (estado is WhatsAppConstantes.Conectado or WhatsAppConstantes.Desconectado)
            _estadoActual.VeniaConectadoAntesDeGracia = false;

        s.EstadoConexion       = estado;
        s.UltimaVerificacionEn = ahora;
        if (!string.IsNullOrWhiteSpace(numeroVinculado)) s.NumeroVinculado = numeroVinculado;

        if (estado == WhatsAppConstantes.Conectado)
        {
            emitir = anterior != WhatsAppConstantes.Conectado;
            s.DesconectadoDesde       = null;
            s.AvisoDesconexionEnviado = false;
        }
        else
        {
            // Solo cuenta como caída si venía de CONECTADO (un arranque ya desconectado no avisa);
            // durante la gracia de arranque tampoco: no fija DesconectadoDesde ni emite aviso.
            if (!enGraciaArranque && veniaConectado && s.DesconectadoDesde is null)
            {
                s.DesconectadoDesde = ahora;
                _estadoActual.VeniaConectadoAntesDeGracia = false;
                emitir = true;
            }
            if (estado == WhatsAppConstantes.Desconectado) s.NumeroVinculado = null;

            if (s.DesconectadoDesde is not null && !s.AvisoDesconexionEnviado && s.Habilitado
                && ahora - s.DesconectadoDesde.Value > WhatsAppConfig.GraciaDesconexion)
            {
                await AvisarDesconexionAsync();
                s.AvisoDesconexionEnviado = true;
                emitir = true;
            }
        }

        s.ActualizadoEn = ahora;
        await _db.SaveChangesAsync();
        _estadoActual.Estado = estado;

        if (anterior != estado)
            _logger.LogInformation("WhatsApp: {Anterior} → {Nuevo} ({Origen})", anterior, estado, origen);

        if (emitir)
        {
            try
            {
                await _hub.Clients.Group("admin").SendAsync("WhatsAppEstado",
                    new { estado, desconectadoDesde = s.DesconectadoDesde });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo emitir WhatsAppEstado por SignalR");
            }
        }
    }

    private async Task AvisarDesconexionAsync()
    {
        try
        {
            var yaAvisado = await _notificaciones.ExisteNoLeidaRecienteAsync(
                "WHATSAPP_DESCONECTADO", null, TimeSpan.FromHours(6));
            if (yaAvisado) return;

            await _notificaciones.CrearParaUsuariosAsync(
                "WHATSAPP_DESCONECTADO",
                "WhatsApp desconectado",
                "Hay que reconectar WhatsApp desde el módulo de WhatsApp.",
                ["ADMIN"],
                "WhatsAppSettings", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo crear la notificación WHATSAPP_DESCONECTADO");
        }
    }
}
