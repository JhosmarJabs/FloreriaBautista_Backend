using System.ComponentModel.DataAnnotations;

namespace FloreriaBautista.Models.DTOs.Database;

public class RestoreRequestDto
{
    /// <summary>ID del archivo en Google Drive a restaurar.</summary>
    [Required]
    public string DriveFileId { get; set; } = string.Empty;

    /// <summary>Confirmación requerida para evitar restauraciones accidentales.</summary>
    [Required]
    public string Confirmacion { get; set; } = string.Empty; // debe ser "CONFIRMAR_RESTAURACION"
}
