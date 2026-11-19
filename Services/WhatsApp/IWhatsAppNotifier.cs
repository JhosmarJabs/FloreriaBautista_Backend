namespace FloreriaBautista.Services.WhatsApp;

/// <summary>
/// Encola avisos de WhatsApp para los destinatarios activos. Nunca lanza y usa su propio
/// DbContext, así que no se mezcla con la transacción del caller. Llamar DESPUÉS del
/// <c>SaveChangesAsync</c> de la operación de negocio.
/// </summary>
public interface IWhatsAppNotifier
{
    /// <returns>Cuántos mensajes se encolaron (0 si está deshabilitado, apagado, deduplicado o sin destinatarios).</returns>
    Task<int> EncolarAsync(string tipoEvento, string texto, string? entidadTipo = null,
        Guid? entidadId = null, TimeSpan? deduplicarVentana = null);

    /// <summary>Mensaje de prueba (ignora los interruptores de eventos). Null = todos los activos.</summary>
    Task<int> EncolarPruebaAsync(Guid? recipientId);
}
