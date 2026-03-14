using System.ComponentModel.DataAnnotations;

namespace FloreriaBautista.Models.DTOs.Database;

public class RestoreRequestDto
{
    /// <summary>ID del archivo en Google Drive a restaurar.</summary>
    [Required]
    public string DriveFileId { get; set; } = string.Empty;

    /// <summary>
    /// Si es true, hace DROP de todas las tablas antes de restaurar (restauración completa).
    /// Si es false, intenta restaurar sin borrar datos existentes.
    /// </summary>
    public bool LimpiarAntes { get; set; } = false;

    /// <summary>Confirmación requerida para evitar restauraciones accidentales.</summary>
    [Required]
    public string Confirmacion { get; set; } = string.Empty; // debe ser "CONFIRMAR_RESTAURACION"
}
