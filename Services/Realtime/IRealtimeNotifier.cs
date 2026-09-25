using FloreriaBautista.Models.DTOs.InstantSales;

namespace FloreriaBautista.Services.Realtime;

public interface IRealtimeNotifier
{
    /// <summary>Notifica al grupo admin que hay una solicitud nueva PENDIENTE.</summary>
    Task SolicitudPendienteAsync(SolicitudVentaInstantaneaDto solicitud);

    /// <summary>Notifica al responsable de turno que se escalaron solicitudes.</summary>
    Task SolicitudEscaladaAsync(SolicitudVentaInstantaneaDto solicitud);

    /// <summary>Notifica al cliente dueño de la solicitud que se tomó una decision.</summary>
    Task SolicitudDecididaAsync(Guid solicitudId, SolicitudVentaInstantaneaDto solicitud);

    /// <summary>Notifica a todos los empleados que se creó un pedido nuevo.</summary>
    Task PedidoNuevoAsync(PedidoNuevoNotificacion notificacion);

    /// <summary>Notifica informativa al admin de un pedido anticipado (>=1 semana).</summary>
    Task PedidoAnticipadoInformativoAsync(PedidoNuevoNotificacion notificacion);
}

public class PedidoNuevoNotificacion
{
    public Guid   OrderId        { get; set; }
    public string NombreCliente  { get; set; } = string.Empty;
    public string TipoPedido     { get; set; } = string.Empty;
    public string FechaEntrega   { get; set; } = string.Empty;
    public string? HoraEntrega   { get; set; }
    public string? Direccion     { get; set; }
    public decimal Total         { get; set; }
    public string? Notas         { get; set; }
    public bool   EsInstantanea  { get; set; }
}
