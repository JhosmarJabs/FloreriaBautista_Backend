using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;

namespace FloreriaBautista.Services.WhatsApp;

/// <summary>
/// Versión sugerida de WhatsApp Web: lee <c>client_revision</c> de <c>https://web.whatsapp.com/sw.js</c>
/// y arma <c>2.3000.{client_revision}</c> (misma técnica que <c>fetchLatestWaWebVersion</c> de Baileys).
/// Caché de 1 h. Si falla devuelve null y el admin la escribe a mano.
/// </summary>
public class WhatsAppVersionProvider
{
    public const string NombreHttpClient = "whatsapp-web";
    private const string CacheKey = "whatsapp:version-sugerida";

    private static readonly Regex RegexRevision =
        new(@"\\?""client_revision\\?"":\s*(\d+)", RegexOptions.Compiled);

    private readonly IHttpClientFactory                _httpFactory;
    private readonly IMemoryCache                      _cache;
    private readonly ILogger<WhatsAppVersionProvider>  _logger;

    public WhatsAppVersionProvider(IHttpClientFactory httpFactory, IMemoryCache cache,
        ILogger<WhatsAppVersionProvider> logger)
    {
        _httpFactory = httpFactory;
        _cache       = cache;
        _logger      = logger;
    }

    public async Task<string?> ObtenerSugeridaAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out string? enCache)) return enCache;

        try
        {
            var http = _httpFactory.CreateClient(NombreHttpClient);
            var js   = await http.GetStringAsync("https://web.whatsapp.com/sw.js", ct);
            var m    = RegexRevision.Match(js);
            if (!m.Success)
            {
                _logger.LogWarning("No se encontró client_revision en sw.js de WhatsApp Web");
                return null;
            }

            var version = $"2.3000.{m.Groups[1].Value}";
            _cache.Set(CacheKey, version, TimeSpan.FromHours(1));
            return version;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogWarning("No se pudo detectar la versión de WhatsApp Web: {Tipo}", ex.GetType().Name);
            return null;
        }
    }
}
