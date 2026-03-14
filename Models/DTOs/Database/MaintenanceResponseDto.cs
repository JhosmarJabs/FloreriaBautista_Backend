namespace FloreriaBautista.Models.DTOs.Database;

public class MaintenanceResponseDto
{
    public string   Tarea          { get; set; } = string.Empty;
    public string   Estado         { get; set; } = string.Empty; // COMPLETADO | ERROR
    public string   Detalle        { get; set; } = string.Empty;
    public string?  MensajeError   { get; set; }
    public DateTime EjecutadoEn   { get; set; } = DateTime.UtcNow;
    public double   DuracionMs     { get; set; }
    public List<string> Resultados { get; set; } = [];
}
