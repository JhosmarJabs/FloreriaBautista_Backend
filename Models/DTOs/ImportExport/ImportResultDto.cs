namespace FloreriaBautista.Models.DTOs.ImportExport;

public class ImportResultDto
{
    public string  Archivo      { get; set; } = string.Empty;
    public int     TotalFilas   { get; set; }
    public int     Insertados   { get; set; }
    public int     Actualizados { get; set; }
    public int     Errores      { get; set; }
    public List<string> DetalleErrores { get; set; } = [];
    public DateTime EjecutadoEn { get; set; } = DateTime.UtcNow;
    public double  DuracionMs   { get; set; }
}
