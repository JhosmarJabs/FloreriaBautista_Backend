namespace FloreriaBautista.Models.Entities;

/// <summary>Persona interna que recibe los avisos de WhatsApp. Sin borrado físico.</summary>
public class WhatsAppRecipient
{
    public Guid     Id            { get; set; } = Guid.NewGuid();
    public string   Nombre        { get; set; } = string.Empty;
    /// <summary>Solo dígitos, con lada de México (52 + 10/11 dígitos).</summary>
    public string   Telefono      { get; set; } = string.Empty;
    public bool     Activo        { get; set; } = true;
    /// <summary>Resultado de <c>whatsappNumbers</c>; null si no se pudo verificar.</summary>
    public bool?    Verificado    { get; set; }
    public DateTime CreadoEn      { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;
}
