using System.ComponentModel.DataAnnotations;

namespace FloreriaBautista.Models.DTOs.Orders;

public class RegisterPaymentRequestDto
{
    [Required] [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a 0.")]
    public decimal Monto  { get; set; }
    [Required] public string Metodo { get; set; } = string.Empty; // EFECTIVO / TARJETA / TRANSFERENCIA / OTRO
}

public class PaymentResponseDto
{
    public Guid     Id        { get; set; }
    public decimal  Monto     { get; set; }
    public string   TipoPago  { get; set; } = string.Empty; // ANTICIPO / TOTAL / LIQUIDACION
    public string   Metodo    { get; set; } = string.Empty;
    public DateTime FechaPago { get; set; }
    public string   Estado    { get; set; } = string.Empty;
}
