namespace FloreriaBautista.Models.DTOs.Database;

public class RestoreResponseDto
{
    public string   Estado            { get; set; } = string.Empty; // COMPLETADO | ERROR
    public string   ArchivoRestaurado { get; set; } = string.Empty;
    public string?  MensajeError      { get; set; }
    public DateTime EjecutadoEn       { get; set; } = DateTime.UtcNow;
    public double   DuracionMs        { get; set; }

    /// <summary>
    /// Filas recuperadas por tabla. Valor -1 indica que esa tabla tuvo error.
    /// </summary>
    public Dictionary<string, int> TablasSincronizadas { get; set; } = [];
}
