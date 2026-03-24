namespace FloreriaBautista.Models.DTOs.Backups;

public class BackupFullRequestDto
{
    /// <summary>Descripción opcional del backup.</summary>
    public string? Descripcion { get; set; }

    /// <summary>Formato del archivo generado: "BACKUP" (pg_dump custom, recomendado) o "SQL" (plain SQL). Por defecto BACKUP.</summary>
    public string Formato { get; set; } = "BACKUP";
}
