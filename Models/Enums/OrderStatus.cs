namespace FloreriaBautista.Models.Enums;

/// <summary>
/// Estados de un pedido y sus agrupaciones. Se guardan como texto en la columna
/// <c>estado_pedido</c>; esta clase existe para que el archivador automático y la
/// consulta de pedidos usen exactamente las mismas listas y no se separen con el
/// tiempo.
/// </summary>
public static class EstadosPedido
{
    public const string PendienteValidacion = "PENDIENTE_VALIDACION";
    public const string EnPreparacion       = "EN_PREPARACION";
    public const string EnRuta              = "EN_RUTA";
    public const string PendienteAnulacion  = "PENDIENTE_ANULACION";
    public const string Entregado           = "ENTREGADO";
    public const string Cancelado           = "CANCELADO";

    /// <summary>Puesto por el archivador cuando nadie atendió el pedido antes de su entrega.</summary>
    public const string NoCompletado = "NO_COMPLETADO";

    /// <summary>
    /// Estados que cerraron el ciclo del pedido. Se incluyen las variantes en
    /// femenino porque hay datos históricos guardados así.
    /// </summary>
    public static readonly string[] Finales =
        [Entregado, "ENTREGADA", Cancelado, "CANCELADA"];

    /// <summary>
    /// Estados en los que un pedido atrasado significa que nadie le dio
    /// seguimiento: el archivador los reescribe a <see cref="NoCompletado"/>.
    /// EN_RUTA queda deliberadamente fuera (ver <c>OrderArchiver</c>).
    /// </summary>
    public static readonly string[] Abandonables =
        [PendienteValidacion, EnPreparacion, PendienteAnulacion];

    /// <summary>
    /// Estados con los que un pedido archivado ya no necesita intervención. Lo que
    /// esté archivado y NO esté aquí es un pedido que "requiere cierre".
    /// </summary>
    public static readonly string[] Cerrados =
        [Entregado, "ENTREGADA", Cancelado, "CANCELADA", NoCompletado];
}
