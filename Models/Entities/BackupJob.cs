namespace FloreriaBautista.Models.Entities;

public class BackupJob
{
    public Guid      Id            { get; set; }
    public string    Tipo          { get; set; } = string.Empty; // BD / BD_ARCHIVOS
    public string    Estado        { get; set; } = "PENDIENTE";
    public Guid?     UsuarioId     { get; set; }
    public DateTime  CreadoEn     { get; set; } = DateTime.UtcNow;
    public DateTime? CompletadoEn { get; set; }
    public string?   MensajeError  { get; set; }

    public User? Usuario { get; set; }
}
