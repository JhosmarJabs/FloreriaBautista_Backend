namespace FloreriaBautista.Models.Entities;

/// <summary>
/// Cola persistente (outbox) e historial de los mensajes de WhatsApp. Una fila por destinatario;
/// la envía <c>WhatsAppDispatcherService</c>.
/// </summary>
public class WhatsAppMessage
{
    public Guid      Id           { get; set; } = Guid.NewGuid();
    public Guid      RecipientId  { get; set; }
    public string    TipoEvento   { get; set; } = string.Empty;
    public string?   EntidadTipo  { get; set; }
    public Guid?     EntidadId    { get; set; }
    public string    Texto        { get; set; } = string.Empty;
    public string    Estado       { get; set; } = "PENDIENTE";
    public int       Intentos     { get; set; }
    public string?   UltimoError  { get; set; }
    public DateTime  CreadoEn     { get; set; } = DateTime.UtcNow;
    public DateTime? EnviadoEn    { get; set; }

    public WhatsAppRecipient Recipient { get; set; } = null!;
}
