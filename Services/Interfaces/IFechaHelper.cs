namespace FloreriaBautista.Services.Interfaces;

/// <summary>
/// Única fuente de "hoy" del backend. Todo lo que compare contra una fecha de
/// negocio (FechaEntrega, cortes diarios, dashboards) debe pasar por aquí en vez
/// de calcular <c>DateOnly.FromDateTime(DateTime.UtcNow)</c>: la florería opera en
/// UTC−6, así que entre las 18:00 y las 23:59 hora local el UTC ya está en el día
/// siguiente y "hoy" en UTC deja de ser el día que ve el usuario.
/// </summary>
public interface IFechaHelper
{
    /// <summary>Zona horaria efectiva de la tienda (configurable en <c>Store:TimeZone</c>).</summary>
    TimeZoneInfo Zona { get; }

    /// <summary>Fecha y hora actuales en la zona de la tienda.</summary>
    DateTime AhoraLocal();

    /// <summary>Día de calendario en curso para la tienda.</summary>
    DateOnly HoyLocal();

    /// <summary>Convierte un instante UTC (p. ej. <c>FechaCreacion</c>) a hora de la tienda.</summary>
    DateTime ALocal(DateTime utc);

    /// <summary>Instante UTC en el que empieza (00:00) un día local de la tienda.</summary>
    DateTime InicioDelDiaUtc(DateOnly fechaLocal);
}
