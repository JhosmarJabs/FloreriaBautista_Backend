namespace FloreriaBautista.Models.DTOs.Notifications;

public class NotificationDto
{
    public Guid     Id          { get; set; }
    public string   Tipo        { get; set; } = string.Empty;
    public string   Titulo      { get; set; } = string.Empty;
    public string   Mensaje     { get; set; } = string.Empty;
    public string?  EntidadTipo { get; set; }
    public Guid?    EntidadId   { get; set; }
    public bool     Leida       { get; set; }
    public DateTime? LeidaEn    { get; set; }
    public DateTime CreadaEn    { get; set; }
}
