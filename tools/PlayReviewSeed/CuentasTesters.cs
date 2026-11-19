using System.Globalization;

namespace FloreriaBautista.PlayReviewSeed;

// TEMPORAL-TESTERS: eliminar cuando Google Play apruebe la app para producción.
//
// Cuentas de los testers de la prueba cerrada de Google Play (12+ testers, 14 días).
// Viven en la base de REVISIÓN (play_review), nunca en producción: el borde de PROD las
// reenvía porque su correo termina en @test.com (REVIEW_LOGIN_EMAIL_DOMAIN de PROD
// debe incluirlo). La contraseña es pública a propósito: son datos de prueba de una
// base sintética. Retirarlas: ver docs/play-review.md, sección "Testers de Google Play".

/// <summary>Cuenta de tester: correo, rol y nombre legible derivado del correo.</summary>
public sealed record CuentaTester(string Rol, string Nombre, string Apellido, string Correo);

public static class CuentasTesters
{
    /// <summary>Contraseña común de los testers (intencionalmente pública).</summary>
    public const string Password = "prueba1234";

    /// <summary>Dominio de las cuentas de testers. PROD debe tenerlo en REVIEW_LOGIN_EMAIL_DOMAIN.</summary>
    public const string Dominio = "@test.com";

    /// <summary>Tipo de id sintético de los testers: 5eed0101-0000-4000-8000-{n}.</summary>
    public const int TipoId = 0x0101;

    private static readonly (string Usuario, string Rol)[] Lista =
    [
        ("gadiel.admin",    "ADMIN"),
        ("liz.prueba",      "EMPLEADO"),
        ("angelito.prueba", "EMPLEADO"),
        ("abi.prueba",      "EMPLEADO"),
        ("alan.prueba",     "EMPLEADO"),
        ("sabino.prueba",   "EMPLEADO"),
        ("domi.prueba",     "EMPLEADO"),
        ("diana.prueba",    "EMPLEADO"),
        ("gadiel.empleado", "EMPLEADO"),
        ("leo.prueba",      "EMPLEADO"),
        ("jordan.prueba",   "EMPLEADO"),
        ("magdiel.prueba",  "EMPLEADO"),
        ("luz.prueba",      "EMPLEADO"),
        ("javier.prueba",   "EMPLEADO"),
        ("paco.prueba",     "EMPLEADO"),
    ];

    /// <summary>
    /// Las cuentas en orden fijo. El orden define el id (n = posición + 1), así que
    /// solo se agrega al final: reordenar cambiaría los ids de cuentas ya sembradas.
    /// </summary>
    public static IReadOnlyList<CuentaTester> Todas { get; } = Lista
        .Select(t =>
        {
            var partes = t.Usuario.Split('.', 2);
            return new CuentaTester(t.Rol, Titulo(partes[0]), Titulo(partes.Length > 1 ? partes[1] : ""),
                (t.Usuario + Dominio).ToLowerInvariant());
        })
        .ToList();

    /// <summary>Id determinista del tester en la posición <paramref name="indice"/> (base 0).</summary>
    public static Guid Id(int indice) => GeneradorDatos.IdSintetico(TipoId, indice + 1);

    private static string Titulo(string s) =>
        string.IsNullOrEmpty(s) ? s : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());
}
