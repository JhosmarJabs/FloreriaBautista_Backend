using FloreriaBautista.Hubs;
using FloreriaBautista.Models.DTOs.InstantSales;
using FloreriaBautista.Services.Notifications;
using Microsoft.AspNetCore.SignalR;

namespace FloreriaBautista.Services.Realtime;

public class RealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<VentaInstantaneaHub> _hub;
    private readonly INotificationService             _notificaciones;
    private readonly ILogger<RealtimeNotifier>         _logger;

    public RealtimeNotifier(
        IHubContext<VentaInstantaneaHub> hub,
        INotificationService notificaciones,
        ILogger<RealtimeNotifier> logger)
    {
        _hub             = hub;
        _notificaciones  = notificaciones;
        _logger          = logger;
    }

    public async Task SolicitudPendienteAsync(SolicitudVentaInstantaneaDto solicitud)
    {
        await PersistirSinFallarAsync(() =>
            _notificaciones.CrearParaUsuariosAsync(
                "VENTA_INSTANTANEA_PENDIENTE",
                "Solicitud de venta instantánea pendiente",
                $"{solicitud.NombreCliente} solicitó {solicitud.NombreProducto} x{solicitud.Cantidad}.",
                ["ADMIN"],
                "SolicitudVentaInstantanea", solicitud.Id));

        _logger.LogInformation(
            "SignalR → grupo admin: SolicitudPendiente {Id}", solicitud.Id);
        await _hub.Clients.Group("admin")
            .SendAsync("SolicitudPendiente", solicitud);
    }

    public async Task SolicitudEscaladaAsync(SolicitudVentaInstantaneaDto solicitud)
    {
        await PersistirSinFallarAsync(() =>
            _notificaciones.CrearParaUsuariosAsync(
                "VENTA_INSTANTANEA_ESCALADA",
                "Solicitud de venta instantánea escalada",
                $"La solicitud de {solicitud.NombreCliente} fue escalada al responsable de turno.",
                ["ADMIN", "EMPLEADO"],
                "SolicitudVentaInstantanea", solicitud.Id));

        _logger.LogInformation(
            "SignalR → grupo responsable: SolicitudEscalada {Id}", solicitud.Id);
        await _hub.Clients.Group("responsable")
            .SendAsync("SolicitudEscalada", solicitud);
    }

    public async Task SolicitudDecididaAsync(
        Guid solicitudId, SolicitudVentaInstantaneaDto solicitud)
    {
        if (solicitud.CustomerId != Guid.Empty)
        {
            var estado = solicitud.Estado?.ToUpper() == "APROBADA" ? "aprobada" : "rechazada";
            await PersistirSinFallarAsync(() =>
                _notificaciones.CrearParaClienteAsync(
                    solicitud.CustomerId,
                    "SOLICITUD_DECIDIDA",
                    $"Tu solicitud de venta instantánea fue {estado}",
                    $"Tu solicitud de {solicitud.NombreProducto} fue {estado}.",
                    "SolicitudVentaInstantanea", solicitudId));
        }

        _logger.LogInformation(
            "SignalR → grupo solicitud-{Id}: SolicitudDecidida ({Estado})",
            solicitudId, solicitud.Estado);
        await _hub.Clients.Group($"solicitud-{solicitudId}")
            .SendAsync("SolicitudDecidida", solicitud);
    }

    public async Task PedidoNuevoAsync(PedidoNuevoNotificacion notificacion)
    {
        await PersistirSinFallarAsync(() =>
            _notificaciones.CrearParaUsuariosAsync(
                "PEDIDO_NUEVO",
                "Nuevo pedido recibido",
                $"{notificacion.NombreCliente} realizó un pedido por ${notificacion.Total:N2}.",
                ["ADMIN", "EMPLEADO"],
                "Order", notificacion.OrderId));

        _logger.LogInformation(
            "SignalR → grupo todos-empleados: PedidoNuevo {OrderId}", notificacion.OrderId);
        await _hub.Clients.Group("todos-empleados")
            .SendAsync("PedidoNuevo", notificacion);
    }

    public async Task PedidoAnticipadoInformativoAsync(PedidoNuevoNotificacion notificacion)
    {
        await PersistirSinFallarAsync(() =>
            _notificaciones.CrearParaUsuariosAsync(
                "PEDIDO_ANTICIPADO",
                "Pedido anticipado recibido",
                $"{notificacion.NombreCliente} agendó un pedido para {notificacion.FechaEntrega} (${notificacion.Total:N2}).",
                ["ADMIN"],
                "Order", notificacion.OrderId));

        _logger.LogInformation(
            "SignalR → grupo admin: PedidoAnticipadoInformativo {OrderId}", notificacion.OrderId);
        await _hub.Clients.Group("admin")
            .SendAsync("PedidoAnticipadoInformativo", notificacion);
    }

    private async Task PersistirSinFallarAsync(Func<Task> persistir)
    {
        try
        {
            await persistir();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al persistir notificación — el push en vivo continuará");
        }
    }
}
