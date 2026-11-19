using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace FloreriaBautista.PlayReview;

public enum ResultadoFirma
{
    Valida,
    Ausente,
    Malformada,
    Invalida,
    Caducada
}

/// <summary>
/// Firma del header <c>X-Play-Review-Edge</c> con la que PROD demuestra a REVIEW que
/// la petición pasó por el borde.
///
/// Formato del header: <c>{ts}.{firma}</c>, donde <c>ts</c> son segundos Unix y
/// <c>firma = base64url(HMAC-SHA256(secreto, ts + "\n" + MÉTODO + "\n" + ruta))</c>.
/// La ruta es <c>PathBase + Path</c> sin query string (el query puede traer el
/// <c>access_token</c> de SignalR y no hace falta firmarlo: lo valida el JWT).
/// </summary>
public static class FirmaBorde
{
    public static string CrearHeader(string secreto, DateTimeOffset ahora, string metodo, string ruta)
    {
        var ts = ahora.ToUnixTimeSeconds();
        return $"{ts.ToString(CultureInfo.InvariantCulture)}.{Firmar(secreto, ts, metodo, ruta)}";
    }

    public static string Firmar(string secreto, long ts, string metodo, string ruta)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secreto));
        var datos = Encoding.UTF8.GetBytes(Canonico(ts, metodo, ruta));
        return Base64Url(hmac.ComputeHash(datos));
    }

    public static ResultadoFirma Validar(
        string secreto, string? header, string metodo, string ruta, DateTimeOffset ahora,
        int toleranciaSegundos = PlayReviewEntorno.ToleranciaFirmaSegundos)
    {
        if (string.IsNullOrWhiteSpace(header)) return ResultadoFirma.Ausente;

        var partes = header.Trim().Split('.');
        if (partes.Length != 2 ||
            !long.TryParse(partes[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ts))
            return ResultadoFirma.Malformada;

        byte[] recibida;
        try { recibida = DesdeBase64Url(partes[1]); }
        catch (FormatException) { return ResultadoFirma.Malformada; }

        // Primero la firma (tiempo constante) y después la ventana de tiempo: así un
        // ts manipulado nunca se reporta como "caducado" sin tener una firma válida.
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secreto));
        var esperada = hmac.ComputeHash(Encoding.UTF8.GetBytes(Canonico(ts, metodo, ruta)));
        if (!CryptographicOperations.FixedTimeEquals(esperada, recibida))
            return ResultadoFirma.Invalida;

        var diferencia = Math.Abs(ahora.ToUnixTimeSeconds() - ts);
        return diferencia > toleranciaSegundos ? ResultadoFirma.Caducada : ResultadoFirma.Valida;
    }

    /// <summary>
    /// Método en mayúsculas. Un WebSocket sobre HTTP/2 llega como CONNECT a un lado y
    /// como GET al otro, así que se normaliza a GET para que la firma no dependa del
    /// protocolo entre los dos servicios.
    /// </summary>
    private static string Canonico(long ts, string metodo, string ruta)
    {
        var m = (metodo ?? string.Empty).Trim().ToUpperInvariant();
        if (m == "CONNECT") m = "GET";
        var r = string.IsNullOrEmpty(ruta) ? "/" : ruta;
        return $"{ts.ToString(CultureInfo.InvariantCulture)}\n{m}\n{r}";
    }

    internal static string Base64Url(byte[] datos) =>
        Convert.ToBase64String(datos).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    internal static byte[] DesdeBase64Url(string texto)
    {
        var s = texto.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
            case 1: throw new FormatException("Base64url inválido.");
        }
        return Convert.FromBase64String(s);
    }
}
