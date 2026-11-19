using System.Text.Json;

namespace FloreriaBautista.PlayReviewSeed;

/// <summary>Cuenta de revisión tal como viene del JSON de credenciales (fuera del repositorio).</summary>
public sealed record CuentaRevision(string Rol, string Nombre, string Apellido, string Correo, string Password);

/// <summary>
/// Lee la sección <c>floreria.cuentas</c> del JSON de credenciales. Nunca imprime
/// contraseñas; solo valida que existan una cuenta ADMIN y una EMPLEADO con el dominio
/// de revisión.
/// </summary>
public static class Credenciales
{
    public const int LongitudMinimaPassword = 12;

    public static IReadOnlyList<CuentaRevision> Leer(string ruta, IReadOnlyList<string> dominiosLogin)
    {
        if (!File.Exists(ruta))
            throw new GuardaException($"No existe el archivo de credenciales: {ruta}");

        using var doc = JsonDocument.Parse(File.ReadAllText(ruta));
        return Desde(doc.RootElement, dominiosLogin);
    }

    public static IReadOnlyList<CuentaRevision> Desde(JsonElement raiz, string dominioLogin) =>
        Desde(raiz, [dominioLogin]);

    public static IReadOnlyList<CuentaRevision> Desde(JsonElement raiz, IReadOnlyList<string> dominiosLogin)
    {
        if (!raiz.TryGetProperty("floreria", out var floreria)
            || !floreria.TryGetProperty("cuentas", out var cuentas)
            || cuentas.ValueKind != JsonValueKind.Array)
            throw new GuardaException("El JSON de credenciales no tiene la sección floreria.cuentas.");

        var lista = new List<CuentaRevision>();
        foreach (var c in cuentas.EnumerateArray())
        {
            string Texto(string nombre) =>
                c.TryGetProperty(nombre, out var v) && v.ValueKind == JsonValueKind.String
                    ? v.GetString()!.Trim()
                    : throw new GuardaException($"Una cuenta de floreria.cuentas no tiene '{nombre}'.");

            var cuenta = new CuentaRevision(
                Texto("rol").ToUpperInvariant(), Texto("nombre"), Texto("apellido"),
                Texto("correo").ToLowerInvariant(), Texto("password"));

            if (cuenta.Rol is not ("ADMIN" or "EMPLEADO"))
                throw new GuardaException($"Rol no permitido para una cuenta de revisión: {cuenta.Rol}.");
            if (!FloreriaBautista.PlayReview.PlayReviewEntorno.CorreoEsDeRevision(cuenta.Correo, dominiosLogin))
                throw new GuardaException(
                    $"La cuenta {cuenta.Correo} no termina en {string.Join(" ni en ", dominiosLogin)}: el borde de PROD no la reconocería.");
            if (cuenta.Password.Length < LongitudMinimaPassword)
                throw new GuardaException($"La contraseña de {cuenta.Correo} es demasiado corta (mínimo {LongitudMinimaPassword}).");

            lista.Add(cuenta);
        }

        if (lista.Count(c => c.Rol == "ADMIN") != 1 || lista.Count(c => c.Rol == "EMPLEADO") != 1)
            throw new GuardaException("Se espera exactamente una cuenta ADMIN y una EMPLEADO en floreria.cuentas.");
        if (lista.Select(c => c.Correo).Distinct().Count() != lista.Count)
            throw new GuardaException("Hay correos repetidos en floreria.cuentas.");

        return lista;
    }
}
