using System.Text;
using System.Text.Json;
using FloreriaBautista.Data;
using FloreriaBautista.Hubs;
using FloreriaBautista.Models.DTOs.InstantSales;
using FloreriaBautista.Services.Notifications;
using FloreriaBautista.Services.WhatsApp;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Services.Realtime;

public class RealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<VentaInstantaneaHub> _hub;
    private readonly INotificationService             _notificaciones;
    private readonly ILogger<RealtimeNotifier>         _logger;
    private readonly IServiceScopeFactory              _scopeFactory;
    private readonly IWhatsAppNotifier                 _whatsApp;

    public RealtimeNotifier(
        IHubContext<VentaInstantaneaHub> hub,
        INotificationService notificaciones,
        ILogger<RealtimeNotifier> logger,
        IServiceScopeFactory scopeFactory,
        IWhatsAppNotifier whatsApp)
    {
        _hub             = hub;
        _notificaciones  = notificaciones;
        _logger          = logger;
        _scopeFactory    = scopeFactory;
        _whatsApp        = whatsApp;
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

        _ = EnviarWhatsAppVentaInstantaneaAsync(
            $"🔔 Venta instantánea pendiente: {solicitud.NombreCliente} solicitó " +
            $"{solicitud.NombreProducto} x{solicitud.Cantidad}. Tienes 5 min para decidir.",
            solicitud.Id);

        _ = EnviarPushVentaInstantaneaAsync(
            "Venta instantánea pendiente",
            $"Nueva solicitud: {solicitud.NombreProducto} x{solicitud.Cantidad}.",
            solicitud.Id, "pendiente");
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

        _ = EnviarWhatsAppVentaInstantaneaAsync(
            $"⚠️ Venta instantánea ESCALADA: la solicitud de {solicitud.NombreCliente} " +
            $"({solicitud.NombreProducto} x{solicitud.Cantidad}) fue escalada al responsable de turno. " +
            $"Quedan ~3 min.",
            solicitud.Id);

        _ = EnviarPushVentaInstantaneaAsync(
            "Venta instantánea ESCALADA",
            $"Solicitud escalada: {solicitud.NombreProducto} x{solicitud.Cantidad}.",
            solicitud.Id, "escalada");
    }

    public async Task SolicitudDecididaAsync(
        Guid solicitudId, SolicitudVentaInstantaneaDto solicitud)
    {
        if (solicitud.CustomerId != Guid.Empty)
        {
            var estado = solicitud.Estado?.ToUpper() == "ACEPTADA" ? "aprobada" : "rechazada";
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

        // La venta de mostrador nace ENTREGADO: solo avisa al admin lo que queda por validar.
        if (!notificacion.EsInstantanea)
            _ = EnviarPushAvisoAsync(
                db => TokensPorRol(db, "ADMIN"),
                "pedidos",
                "Pedido nuevo por validar",
                $"Pedido de ${notificacion.Total:N2} para el {notificacion.FechaEntrega}.",
                new { tipo = "pedido_nuevo", pedidoId = notificacion.OrderId.ToString() },
                $"pedido nuevo {notificacion.OrderId}");
    }

    public async Task ReporteErrorCreadoAsync(Guid reporteId, string tipoRegistro, string nombreEmpleado)
    {
        var registro = tipoRegistro.ToUpperInvariant() switch
        {
            "VENTA" => "una venta",
            "GASTO" => "un gasto",
            "CORTE" => "un corte de caja",
            _       => "un registro",
        };

        await PersistirSinFallarAsync(() =>
            _notificaciones.CrearParaUsuariosAsync(
                "REPORTE_ERROR",
                "Nuevo reporte de error",
                $"{nombreEmpleado} reportó un error en {registro}.",
                ["ADMIN"],
                "ErrorReport", reporteId));

        _ = EnviarPushAvisoAsync(
            db => TokensPorRol(db, "ADMIN"),
            "reportes",
            "Nuevo reporte de error",
            $"Un empleado reportó un error en {registro}.",
            new { tipo = "reporte_error", reporteErrorId = reporteId.ToString() },
            $"reporte de error {reporteId}");
    }

    public async Task PedidoCambioEstadoAsync(Guid orderId, Guid empleadoId, string nuevoEstado)
    {
        var estado = nuevoEstado switch
        {
            "EN_PREPARACION"      => "en preparación",
            "EN_RUTA"             => "en ruta",
            "ENTREGADO"           => "entregado",
            "CANCELADO"           => "cancelado",
            "PENDIENTE_ANULACION" => "pendiente de anulación",
            _                     => nuevoEstado.ToLowerInvariant(),
        };

        await PersistirSinFallarAsync(() =>
            _notificaciones.CrearParaUsuarioAsync(
                empleadoId,
                "PEDIDO_ESTADO_CAMBIADO",
                "Cambió el estado de tu pedido",
                $"Uno de tus pedidos pasó a {estado}.",
                "Order", orderId));

        _ = EnviarPushAvisoAsync(
            db => db.PushTokenDevices
                .Where(p => p.UserId == empleadoId && p.User.Estado == "ACTIVO")
                .Select(p => p.Token),
            "pedidos",
            "Cambió el estado de tu pedido",
            $"Uno de tus pedidos pasó a {estado}.",
            new { tipo = "pedido_estado", pedidoId = orderId.ToString(), estado = nuevoEstado },
            $"pedido {orderId} → {nuevoEstado}");
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

    // WhatsApp es solo informativo y de mejor esfuerzo: se encola en el servicio propio
    // (antes iba a un webhook de n8n). El notifier abre su propio scope y nunca lanza.
    private async Task EnviarWhatsAppVentaInstantaneaAsync(string mensaje, Guid solicitudId)
    {
        try
        {
            await _whatsApp.EncolarAsync(WhatsAppConstantes.EventoVentaInstantanea, mensaje,
                "SolicitudVentaInstantanea", solicitudId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al encolar WhatsApp de venta instantánea — no se bloquea el flujo");
        }
    }

    // Mensaje de solo datos (sin title/body en la raíz): así expo-notifications no lo dibuja
    // y lo atiende la tarea en segundo plano de mobile-admin, que publica la alerta a pantalla
    // completa (Nivel 2) o la notificación normal. El texto no lleva datos del cliente: el
    // resto se pide con autenticación al abrir la solicitud.
    private Task EnviarPushVentaInstantaneaAsync(
        string titulo, string cuerpo, Guid solicitudId, string evento)
        => EnviarPushAsync(
            db => TokensPorRol(db, "ADMIN", "EMPLEADO"),
            token => new
            {
                to = token,
                priority = "high",
                channelId = "venta-instantanea",
                data = new
                {
                    tipo = "venta_instantanea",
                    solicitudId = solicitudId.ToString(),
                    evento,
                    titulo,
                    cuerpo
                }
            },
            $"solicitud {solicitudId}, {evento}");

    // Aviso normal (title/body en la raíz): lo dibuja expo-notifications en el canal
    // indicado, y al tocarlo mobile-admin navega según los ids de `data`.
    private Task EnviarPushAvisoAsync(
        Func<AppDbContext, IQueryable<string>> destinatarios, string canal,
        string titulo, string cuerpo, object data, string descripcion)
        => EnviarPushAsync(
            destinatarios,
            token => new
            {
                to = token,
                priority = "high",
                channelId = canal,
                sound = "default",
                title = titulo,
                body = cuerpo,
                data
            },
            descripcion);

    // «Sesión abierta» = el token sigue registrado (se borra al cerrar sesión).
    private static IQueryable<string> TokensPorRol(AppDbContext db, params string[] roles)
        => db.PushTokenDevices
            .Where(p => p.User.Estado == "ACTIVO"
                && p.User.UserRoles.Any(ur => roles.Contains(ur.Role.Nombre)))
            .Select(p => p.Token);

    private async Task EnviarPushAsync(
        Func<AppDbContext, IQueryable<string>> destinatarios,
        Func<string, object> crearMensaje, string descripcion)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var tokens = await destinatarios(db).Distinct().ToListAsync();

            if (tokens.Count == 0)
            {
                _logger.LogInformation("Push Expo → sin tokens registrados ({Descripcion}), se omite el envío",
                    descripcion);
                return;
            }

            var messages = tokens.Select(crearMensaje).ToList();

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            var payload = JsonSerializer.Serialize(messages);
            var httpContent = new StringContent(payload, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("https://exp.host/--/api/v2/push/send", httpContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Push Expo → respuesta {StatusCode}", response.StatusCode);
                return;
            }

            _logger.LogInformation(
                "Push Expo → enviado a {Count} dispositivos ({Descripcion})",
                tokens.Count, descripcion);

            // Los tickets vienen en el mismo orden que los mensajes.
            var muertos = TokensNoRegistrados(await response.Content.ReadAsStringAsync(), tokens);
            if (muertos.Count > 0)
            {
                var borrados = await db.PushTokenDevices
                    .Where(p => muertos.Contains(p.Token))
                    .ExecuteDeleteAsync();
                _logger.LogInformation(
                    "Push Expo → {Count} tokens con DeviceNotRegistered eliminados", borrados);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar push Expo — no se bloquea el flujo");
        }
    }

    private List<string> TokensNoRegistrados(string respuesta, List<string> tokens)
    {
        var muertos = new List<string>();
        using var json = JsonDocument.Parse(respuesta);
        if (!json.RootElement.TryGetProperty("data", out var tickets)
            || tickets.ValueKind != JsonValueKind.Array)
            return muertos;

        var i = 0;
        foreach (var ticket in tickets.EnumerateArray())
        {
            if (i >= tokens.Count) break;
            if (ticket.TryGetProperty("status", out var status) && status.GetString() == "error")
            {
                var error = ticket.TryGetProperty("details", out var details)
                    && details.TryGetProperty("error", out var e) ? e.GetString() : null;
                if (error == "DeviceNotRegistered")
                    muertos.Add(tokens[i]);
                else
                    _logger.LogWarning("Push Expo → error en el ticket: {Error}", error ?? ticket.ToString());
            }
            i++;
        }
        return muertos;
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
