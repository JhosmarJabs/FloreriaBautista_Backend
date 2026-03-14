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

    public Order Order { get; set; } = null!;
}
