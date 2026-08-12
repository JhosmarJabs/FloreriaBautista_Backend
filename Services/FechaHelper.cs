using FloreriaBautista.Services.Interfaces;

namespace FloreriaBautista.Services;

/// <summary>
/// Implementación de <see cref="IFechaHelper"/>. Se registra como singleton: la
/// zona se resuelve una sola vez al arrancar.
///
/// La zona se toma de la configuración <c>Store:TimeZone</c> (o de la variable de
/// entorno <c>STORE_TIMEZONE</c>). El identificador correcto depende del sistema:
/// en Linux/Docker es IANA (<c>America/Mexico_City</c>) y en Windows es
/// <c>Central Standard Time (Mexico)</c>. .NET 8 acepta ambos en las dos
/// plataformas gracias a ICU, pero se prueban en cadena por si el contenedor
/// viniera sin la base de datos de zonas horarias; el último recurso es un offset
/// fijo de UTC−6 (México Central ya no aplica horario de verano desde 2022).
/// </summary>
public class FechaHelper : IFechaHelper
{
    public const string ZonaIanaPorDefecto    = "America/Mexico_City";
    public const string ZonaWindowsPorDefecto = "Central Standard Time (Mexico)";

    private readonly Func<DateTime> _ahoraUtc;

    public TimeZoneInfo Zona { get; }

    public FechaHelper(IConfiguration config, ILogger<FechaHelper> logger)
        : this(ResolverZona(
            config["Store:TimeZone"] ?? Environment.GetEnvironmentVariable("STORE_TIMEZONE"),
            logger))
    {
    }

    /// <summary>
    /// Constructor directo, pensado para pruebas: permite fijar la zona y el reloj
    /// ("ahora" en UTC) sin depender de la hora real de la máquina.
    /// </summary>
    public FechaHelper(TimeZoneInfo zona, Func<DateTime>? ahoraUtc = null)
    {
        Zona      = zona;
        _ahoraUtc = ahoraUtc ?? (() => DateTime.UtcNow);
    }

    public DateTime AhoraLocal() => ALocal(_ahoraUtc());

    public DateOnly HoyLocal() => DateOnly.FromDateTime(AhoraLocal());

    public DateTime ALocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zona);

    public DateTime InicioDelDiaUtc(DateOnly fechaLocal)
    {
        var local = fechaLocal.ToDateTime(TimeOnly.MinValue);
        // Se usa GetUtcOffset en vez de ConvertTimeToUtc porque este último lanza
        // excepción en las horas inexistentes de un cambio de horario de verano.
        return DateTime.SpecifyKind(local - Zona.GetUtcOffset(local), DateTimeKind.Utc);
    }

    private static TimeZoneInfo ResolverZona(string? idConfigurado, ILogger logger)
    {
        var candidatos = new[] { idConfigurado, ZonaIanaPorDefecto, ZonaWindowsPorDefecto }
            .Where(id => !string.IsNullOrWhiteSpace(id));

        foreach (var id in candidatos)
        {
            try
            {
                var zona = TimeZoneInfo.FindSystemTimeZoneById(id!);
                logger.LogInformation("Zona horaria de la tienda: {Zona} (offset {Offset}).",
                    zona.Id, zona.GetUtcOffset(DateTime.UtcNow));
                return zona;
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                logger.LogWarning("Zona horaria '{Zona}' no disponible en este sistema; se prueba la siguiente.", id);
            }
        }

        logger.LogWarning("Ninguna zona horaria conocida está disponible; se usa un offset fijo de UTC-6.");
        return TimeZoneInfo.CreateCustomTimeZone(
            "Floreria-UTC-6", TimeSpan.FromHours(-6), "Florería (UTC-6)", "Florería (UTC-6)");
    }
}
