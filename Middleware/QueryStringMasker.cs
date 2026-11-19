using System.Text.RegularExpressions;

namespace FloreriaBautista.Middleware;

/// <summary>
/// Oculta el JWT que SignalR envía como <c>?access_token=</c> (conexión WebSocket al hub)
/// antes de escribir la query en logs o trazas. El resto de la query se conserva.
/// </summary>
internal static class QueryStringMasker
{
    private static readonly Regex AccessTokenRegex = new(
        @"(?<=[?&]access_token=)[^&]*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Mask(QueryString query)
    {
        var value = query.Value;
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return AccessTokenRegex.Replace(value, "***");
    }
}
