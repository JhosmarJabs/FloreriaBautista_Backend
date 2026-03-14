namespace FloreriaBautista.Models.DTOs.Backups;

/// <summary>Representa un archivo de backup almacenado en Google Drive.</summary>
public class DriveFileDto
{
    public string    Id          { get; set; } = string.Empty;
    public string    Nombre      { get; set; } = string.Empty;
    public long?     TamanoBytes { get; set; }
    public DateTime? CreadoEn   { get; set; }
    public string?   Enlace      { get; set; }
}
