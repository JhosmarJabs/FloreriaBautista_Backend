namespace FloreriaBautista.Models.DTOs.Reports;

/// <summary>
/// Cabecera común a todos los reportes: qué rango se consultó y de dónde salieron
/// los números. `Fuente` se muestra tal cual en la interfaz para que cualquiera
/// pueda auditar el dato sin abrir el código.
/// </summary>
public class ReportHeaderDto
{
    public DateOnly Desde  { get; set; }
    public DateOnly Hasta  { get; set; }
    public string   Fuente { get; set; } = string.Empty;
}

/// <summary>
/// Envoltura de un reporte que puede no estar disponible todavía porque le falta
/// la fuente de datos (una tabla que aún no existe, instrumentación no activada…).
/// Nunca se rellena con ceros ni con datos inventados: si Disponible es false,
/// Datos viene en null y Motivo explica qué falta.
/// </summary>
public class ReportEnvelopeDto<T>
{
    public bool            Disponible { get; set; } = true;
    public string?         Motivo     { get; set; }
    public ReportHeaderDto Encabezado { get; set; } = new();
    public T?              Datos      { get; set; }

    public static ReportEnvelopeDto<T> Ok(T datos, DateOnly desde, DateOnly hasta, string fuente)
        => new()
        {
            Disponible = true,
            Datos      = datos,
            Encabezado = new ReportHeaderDto { Desde = desde, Hasta = hasta, Fuente = fuente }
        };

    public static ReportEnvelopeDto<T> Pendiente(string motivo, DateOnly desde, DateOnly hasta, string fuente)
        => new()
        {
            Disponible = false,
            Motivo     = motivo,
            Encabezado = new ReportHeaderDto { Desde = desde, Hasta = hasta, Fuente = fuente }
        };
}

/// Un punto de una serie temporal (día, semana o mes según la granularidad pedida).
public class TimeBucketDto
{
    public string   Etiqueta { get; set; } = string.Empty;
    public DateOnly Inicio   { get; set; }
    public DateOnly Fin      { get; set; }
    public int      Cantidad { get; set; }
    public decimal  Monto    { get; set; }
}

/// Un corte por categoría (canal, tipo, método de pago, motivo…).
public class BreakdownDto
{
    public string  Clave      { get; set; } = string.Empty;
    public int     Cantidad   { get; set; }
    public decimal Monto      { get; set; }
    /// Participación sobre el total del corte, 0-100. Redondeado a 1 decimal.
    public decimal Porcentaje { get; set; }
}

/// Comparativa de una métrica contra el periodo anterior de la misma duración.
/// `VariacionPct` es null cuando el periodo anterior fue cero: no existe un
/// porcentaje de cambio contra cero y mostrar "+100%" sería inventarlo.
public class ComparisonDto
{
    public decimal  Actual       { get; set; }
    public decimal  Anterior     { get; set; }
    public decimal? VariacionPct { get; set; }

    public static ComparisonDto De(decimal actual, decimal anterior) => new()
    {
        Actual       = actual,
        Anterior     = anterior,
        VariacionPct = anterior == 0 ? null : Math.Round((actual - anterior) / anterior * 100, 1)
    };
}
