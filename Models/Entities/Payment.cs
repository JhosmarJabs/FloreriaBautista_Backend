namespace FloreriaBautista.Models.Entities;

public class Payment
{
    public Guid     Id        { get; set; }
    public Guid     OrderId   { get; set; }
    public decimal  Monto     { get; set; }
    public string   TipoPago  { get; set; } = string.Empty; // ANTICIPO / TOTAL / LIQUIDACION
    public string   Metodo    { get; set; } = string.Empty; // EFECTIVO / TRANSFERENCIA / TARJETA / OTRO
    public DateTime FechaPago { get; set; } = DateTime.UtcNow;
    public string   Estado    { get; set; } = "REGISTRADO";
    // UUID que genera el cliente: un reintento con la misma clave no duplica el pago.
    public Guid?    IdLocalOffline         { get; set; }
    // Quien cobró. Es lo que cuenta el corte de caja; null en pagos del sistema (Mercado Pago).
    public Guid?    RegistradoPorUsuarioId { get; set; }

    public Order Order { get; set; } = null!;
    public User? RegistradoPor { get; set; }
}
