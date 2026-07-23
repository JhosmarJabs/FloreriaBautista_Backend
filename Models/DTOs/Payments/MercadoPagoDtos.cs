namespace FloreriaBautista.Models.DTOs.Payments;

// Solicitud del frontend para iniciar el pago de una orden ya creada.
public class CreatePreferenceRequestDto
{
    public Guid OrderId { get; set; }
}

// Respuesta con la URL de Checkout Pro a la que se redirige al cliente.
public class PreferenceResponseDto
{
    public string PreferenceId { get; set; } = string.Empty;
    public string InitPoint    { get; set; } = string.Empty;
}

// El frontend, al regresar de Mercado Pago, manda el payment_id para confirmar
// el pago de forma sincrónica (fallback que funciona en local sin webhook público).
public class ConfirmPaymentRequestDto
{
    public string PaymentId { get; set; } = string.Empty;
}

// Resultado del procesamiento de un pago (usado por confirm y webhook).
public class PaymentResultDto
{
    public Guid    OrderId  { get; set; }
    public string  Estado   { get; set; } = string.Empty; // approved / pending / rejected / ...
    public bool    Acreditado { get; set; }               // true si se registró el pago en la orden
    public decimal Monto    { get; set; }
}
