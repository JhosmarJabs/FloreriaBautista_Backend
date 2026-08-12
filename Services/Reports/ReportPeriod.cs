namespace FloreriaBautista.Services.Reports;

/// <summary>
/// Rango de fechas de un reporte, resuelto en la zona horaria del negocio.
///
/// Todos los reportes reciben el mismo par (desde, hasta) como fechas LOCALES
/// inclusivas y aquí se traducen a la ventana UTC [DesdeUtc, HastaUtcExclusivo)
/// que se compara contra las columnas timestamp (FechaCreacion, FechaHora,
/// FechaPago…), que están almacenadas en UTC. Sin esta conversión, un pedido
/// capturado a las 7pm en México cae en el día siguiente y el corte del día
/// nunca cuadra con lo que ve el mostrador.
///
/// Las columnas que ya son DateOnly (FechaEntrega, FechaProgramada) se comparan
/// directamente contra Desde/Hasta: no tienen hora y por tanto no tienen zona.
/// </summary>
public readonly record struct ReportPeriod(DateOnly Desde, DateOnly Hasta)
{
    /// México Central. Sin horario de verano desde 2022, por eso alcanza con un
    /// offset fijo en vez de una base de datos de zonas horarias.
    public const int OffsetHorasLocal = -6;

    public DateTime DesdeUtc => DateTime.SpecifyKind(
        Desde.ToDateTime(TimeOnly.MinValue).AddHours(-OffsetHorasLocal), DateTimeKind.Utc);

    /// Límite superior EXCLUSIVO: medianoche local del día siguiente a Hasta.
    public DateTime HastaUtcExclusivo => DateTime.SpecifyKind(
        Hasta.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-OffsetHorasLocal), DateTimeKind.Utc);

    public int Dias => Hasta.DayNumber - Desde.DayNumber + 1;

    /// Periodo inmediatamente anterior, de la misma duración. Es contra el que se
    /// calculan las comparativas ("vs periodo anterior").
    public ReportPeriod Anterior => new(Desde.AddDays(-Dias), Desde.AddDays(-1));

    public static DateOnly HoyLocal =>
        DateOnly.FromDateTime(DateTime.UtcNow.AddHours(OffsetHorasLocal));

    /// Convierte un instante UTC de la base a la fecha local del negocio.
    public static DateOnly ALocal(DateTime utc) =>
        DateOnly.FromDateTime(utc.AddHours(OffsetHorasLocal));

    /// <summary>
    /// Resuelve los parámetros de la query. Si no llegan, usa los últimos 30 días
    /// terminando hoy. Si vienen invertidos, los ordena en vez de devolver vacío.
    /// </summary>
    public static ReportPeriod Resolver(DateOnly? desde, DateOnly? hasta)
    {
        var h = hasta ?? HoyLocal;
        var d = desde ?? h.AddDays(-29);
        return d > h ? new ReportPeriod(h, d) : new ReportPeriod(d, h);
    }

    public bool Contiene(DateOnly fecha) => fecha >= Desde && fecha <= Hasta;
}

/// Granularidad de las series temporales de un reporte.
public enum ReportGranularity { Dia, Semana, Mes }

public static class ReportGranularityExtensions
{
    public static ReportGranularity Parse(string? valor) => valor?.Trim().ToLowerInvariant() switch
    {
        "semana" => ReportGranularity.Semana,
        "mes"    => ReportGranularity.Mes,
        _        => ReportGranularity.Dia
    };

    /// Fecha con la que se agrupa: el propio día, el lunes de su semana, o el día 1 de su mes.
    public static DateOnly InicioDeBucket(this ReportGranularity g, DateOnly fecha) => g switch
    {
        ReportGranularity.Semana => fecha.AddDays(-((int)fecha.DayOfWeek + 6) % 7), // lunes
        ReportGranularity.Mes    => new DateOnly(fecha.Year, fecha.Month, 1),
        _                        => fecha
    };

    public static DateOnly SiguienteBucket(this ReportGranularity g, DateOnly inicio) => g switch
    {
        ReportGranularity.Semana => inicio.AddDays(7),
        ReportGranularity.Mes    => inicio.AddMonths(1),
        _                        => inicio.AddDays(1)
    };

    public static string Etiqueta(this ReportGranularity g, DateOnly inicio) => g switch
    {
        ReportGranularity.Semana => $"{inicio:yyyy-MM-dd}",
        ReportGranularity.Mes    => $"{inicio:yyyy-MM}",
        _                        => $"{inicio:yyyy-MM-dd}"
    };
}
