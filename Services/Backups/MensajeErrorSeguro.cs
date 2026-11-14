namespace FloreriaBautista.Services.Backups;

/// <summary>
/// Quita de un mensaje de error los valores sensibles de configuración (llaves, secretos,
/// contraseñas) antes de guardarlo, devolverlo por la API o registrarlo. Conserva el resto del
/// texto, que es el motivo del fallo.
/// </summary>
public static class MensajeErrorSeguro
{
    public const string Marca = "[REDACTADO]";

    /// <summary>Longitud mínima de un secreto para redactarlo (evita destrozar el texto con valores triviales).</summary>
    private const int LongitudMinima = 6;

    public static string? Redactar(string? mensaje, params string?[] secretos)
    {
        if (string.IsNullOrEmpty(mensaje) || secretos is null)
            return mensaje;

        var resultado = mensaje;
        // Los más largos primero: si un secreto contiene a otro, se redacta completo.
        foreach (var secreto in secretos
                     .Where(s => !string.IsNullOrEmpty(s) && s.Length >= LongitudMinima)
                     .Distinct(StringComparer.Ordinal)
                     .OrderByDescending(s => s!.Length))
        {
            resultado = resultado.Replace(secreto!, Marca, StringComparison.OrdinalIgnoreCase);
        }
        return resultado;
    }

    /// <summary>Valores sensibles que el módulo de respaldos maneja, leídos del entorno.</summary>
    public static string?[] SecretosDeRespaldos() => new[]
    {
        Environment.GetEnvironmentVariable("CLOUDINARY_API_KEY"),
        Environment.GetEnvironmentVariable("CLOUDINARY_API_SECRET"),
        Environment.GetEnvironmentVariable("BACKUP_DB_PASSWORD"),
        Environment.GetEnvironmentVariable("DB_ADMIN_PASSWORD"),
        Environment.GetEnvironmentVariable("DB_PASSWORD")
    };

    /// <summary>Atajo: redacta con los secretos de respaldos tomados del entorno.</summary>
    public static string? Redactar(string? mensaje) => Redactar(mensaje, SecretosDeRespaldos());
}
