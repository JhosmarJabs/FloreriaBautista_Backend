namespace FloreriaBautista.Models.Entities;

/// <summary>
/// Intento de compra con entrega inmediata, sujeto a validacion de stock y
/// aprobacion humana antes de que exista un <see cref="Order"/>.
/// Estados: PENDIENTE -> ACEPTADA -> (Order) | RECHAZADA | EXPIRADA.
/// </summary>
public class SolicitudVentaInstantanea
{
    public Guid     Id                     { get; set; }
    public Guid     CustomerId             { get; set; }
    public Guid     ProductId              { get; set; }
    public int      Cantidad               { get; set; }
    public string   Estado                 { get; set; } = "PENDIENTE"; // PENDIENTE/ACEPTADA/RECHAZADA/EXPIRADA
    public DateTime CreadaEn               { get; set; } = DateTime.UtcNow;
    public DateTime? EscaladaAEmpleadoEn   { get; set; }
    public DateTime? DecididaEn            { get; set; }
    public Guid?    DecididaPorUsuarioId   { get; set; }
    public string?  MotivoRechazo          { get; set; }
    public string?  MotivoExpiracion       { get; set; } // SIN_RESPUESTA / RESERVA_VENCIDA_SIN_PAGO
    public DateTime? ReservaExpiraEn       { get; set; }
    public Guid?    OrderId                { get; set; }

    // -- Navegacion --
    public Customer  Customer    { get; set; } = null!;
    public Product   Product     { get; set; } = null!;
    public User?     DecididaPor { get; set; }
    public Order?    Order       { get; set; }
}
