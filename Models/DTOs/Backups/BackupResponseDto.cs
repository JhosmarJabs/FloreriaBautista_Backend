namespace FloreriaBautista.Models.DTOs.Backups;

public class BackupResponseDto
{
    public Guid      Id              { get; set; }
    public string    Tipo            { get; set; } = string.Empty; // BD | BD_ARCHIVOS
    public string    Formato         { get; set; } = "BACKUP";    // BACKUP | SQL
    public string?   NombreTabla     { get; set; }
    public string    Estado          { get; set; } = string.Empty;
    public string?   Descripcion     { get; set; }
    public string?   MensajeError    { get; set; }
    public DateTime  CreadoEn       { get; set; }
    public DateTime? CompletadoEn   { get; set; }

    // ── Archivo local ──────────────────────────────────────────────
    public string? RutaArchivoLocal { get; set; }
    public long?   TamanoBytes      { get; set; }

    // ── Google Drive ───────────────────────────────────────────────
    public string? DriveFileId   { get; set; }
    public string? DriveEnlace   { get; set; }
    public bool    SubidoADrive  { get; set; } = false;

    // ── Cloudinary ────────────────────────────────────────────────
    public string? CloudinaryPublicId { get; set; }
    public string? CloudinaryEnlace   { get; set; }
    public bool    SubidoACloudinary  { get; set; } = false;
}
