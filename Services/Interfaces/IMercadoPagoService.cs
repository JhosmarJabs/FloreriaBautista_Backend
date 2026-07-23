using FloreriaBautista.Models.DTOs.Payments;

namespace FloreriaBautista.Services.Interfaces;

// Un renglón de la preferencia de pago (producto o envío).
public record MpPreferenceItem(string Title, int Quantity, decimal UnitPrice);

// Datos crudos que devuelve Mercado Pago al consultar un pago.
public record MpPaymentInfo(string Id, string Status, string? ExternalReference, decimal Amount);

public interface IMercadoPagoService
{
    // ¿Hay Access Token configurado? (para responder claro si falta)
    bool EstaConfigurado { get; }

    // Crea una preferencia de Checkout Pro y devuelve la URL de pago (init_point).
    Task<PreferenceResponseDto> CrearPreferenciaAsync(
        Guid orderId, string descripcion, List<MpPreferenceItem> items, string? payerEmail);

    // Consulta el estado de un pago por su id.
    Task<MpPaymentInfo?> ConsultarPagoAsync(string paymentId);

    // Busca en Mercado Pago un pago APROBADO asociado a una orden (external_reference).
    // Sirve para confirmar el pago cuando no hubo redirección con payment_id ni webhook.
    Task<MpPaymentInfo?> BuscarPagoAprobadoPorOrdenAsync(Guid orderId);
}
