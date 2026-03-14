namespace FloreriaBautista.Models.DTOs.Backups;

public class BackupResponseDto
{
    public Guid      Id              { get; set; }
    public string    Tipo            { get; set; } = string.Empty; // FULL | TABLE
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
    public string? DriveFileId   { get; set; }   // ID en Drive
    public string? DriveEnlace   { get; set; }   // URL para ver/descargar desde Drive
    public bool    SubidoADrive  { get; set; } = false;
}
