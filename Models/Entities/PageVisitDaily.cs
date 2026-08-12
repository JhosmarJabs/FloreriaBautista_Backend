namespace FloreriaBautista.Models.Entities;

/// <summary>
/// Agregado diario de <see cref="PageVisit"/>. El detalle se purga a los 90 días;
/// esta tabla se conserva de forma permanente para que las series históricas del
/// reporte de visitas no se corten.
///
/// Una fila por (Fecha, Ruta, Dispositivo).
/// </summary>
public class PageVisitDaily
{
    public Guid     Id          { get; set; }
    public DateOnly Fecha       { get; set; }
    public string   Ruta        { get; set; } = string.Empty;
    public string   Dispositivo { get; set; } = "DESKTOP";

    /// Total de vistas registradas ese día para esa ruta y dispositivo.
    public int      Visitas     { get; set; }

    /// Sesiones distintas que generaron esas vistas.
    public int      Sesiones    { get; set; }

    public DateTime CalculadoEn { get; set; } = DateTime.UtcNow;
}
