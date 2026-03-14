using System.ComponentModel.DataAnnotations;

namespace FloreriaBautista.Models.DTOs.Backups;

/// <summary>
/// Request para crear un respaldo.
/// - Tipo "FULL"  → respalda toda la base de datos (pg_dump completo).
/// - Tipo "TABLE" → respalda solo la tabla indicada en NombreTabla.
/// </summary>
public class BackupRequestDto
{
    [Required]
    public string Tipo { get; set; } = string.Empty; // FULL | TABLE

    /// <summary>Solo requerido cuando Tipo = "TABLE".</summary>
    public string? NombreTabla { get; set; }

    public string? Descripcion { get; set; }
}
