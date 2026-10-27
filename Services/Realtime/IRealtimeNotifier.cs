using FloreriaBautista.Models.DTOs.InstantSales;

namespace FloreriaBautista.Services.Realtime;

public interface IRealtimeNotifier
{
    /// <summary>Notifica al grupo admin que hay una solicitud nueva PENDIENTE.</summary>
    Task SolicitudPendienteAsync(SolicitudVentaInstantaneaDto solicitud);

    /// <summary>Notifica al responsable de turno (<paramref name="responsableId"/>) que se escaló una solicitud.</summary>
    Task SolicitudEscaladaAsync(SolicitudVentaInstantaneaDto solicitud, Guid responsableId);

    /// <summary>Notifica al cliente dueño de la solicitud que se tomó una decision.</summary>
    Task SolicitudDecididaAsync(Guid solicitudId, SolicitudVentaInstantaneaDto solicitud);

    /// <summary>Notifica a todos los empleados que se creó un pedido nuevo.</summary>
    Task PedidoNuevoAsync(PedidoNuevoNotificacion notificacion);

    /// <summary>Notifica informativa al admin de un pedido anticipado (>=1 semana).</summary>
    Task PedidoAnticipadoInformativoAsync(PedidoNuevoNotificacion notificacion);

    /// <summary>Avisa al admin que un empleado reportó un error en un registro suyo.</summary>
    Task ReporteErrorCreadoAsync(Guid reporteId, string tipoRegistro, string nombreEmpleado);

    /// <summary>Avisa al empleado que atendió el pedido que otra persona le cambió el estado.</summary>
    Task PedidoCambioEstadoAsync(Guid orderId, Guid empleadoId, string nuevoEstado);
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
