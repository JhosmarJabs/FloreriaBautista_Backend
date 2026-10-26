using System.Text.RegularExpressions;

namespace FloreriaBautista.Helpers;

/// <summary>
/// Validacion y lectura de ventanas "MM-DD" (festividades/temporadas de un catalogo).
/// </summary>
public static class MesDia
{
    public const string MensajeInvalido =
        "La ventana de la festividad debe tener formato MM-DD válido (ej. 02-14).";

    private static readonly Regex Formato = new(@"^\d{2}-\d{2}$", RegexOptions.Compiled);

    /// <summary>Convierte vacio/espacios en null; recorta el resto.</summary>
    public static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    /// <summary>True si es "MM-DD" con mes 1-12 y dia valido (02-29 es valido).</summary>
    public static bool EsValido(string? valor) => TryParse(valor, out _, out _);

    public static bool TryParse(string? valor, out int mes, out int dia)
    {
        mes = 0; dia = 0;
        if (valor == null || !Formato.IsMatch(valor)) return false;
        mes = int.Parse(valor.AsSpan(0, 2));
        dia = int.Parse(valor.AsSpan(3, 2));
        if (mes < 1 || mes > 12) return false;
        // 2024 = anio bisiesto de referencia, para que 02-29 sea valido.
        return dia >= 1 && dia <= DateTime.DaysInMonth(2024, mes);
    }
}
