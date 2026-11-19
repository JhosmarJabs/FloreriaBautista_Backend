using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FloreriaBautista.Services.WhatsApp;

/// <summary>
/// Cliente tipado de Evolution API v2 (registrado con <c>AddHttpClient</c>: BaseAddress =
/// <c>EVOLUTION_API_URL</c>, cabecera <c>apikey</c>, timeout de 15 s).
/// Los contratos siguen la documentación de v2; las respuestas se leen con tolerancia porque
/// cambiaron de forma entre versiones menores (p. ej. <c>fetchInstances</c>).
/// Regla: el QR (<c>base64</c>) y el <c>pairingCode</c> nunca se escriben en logs.
/// </summary>
public class EvolutionClient : IEvolutionClient
{
    private readonly HttpClient               _http;
    private readonly WhatsAppConfig           _config;
    private readonly ILogger<EvolutionClient> _logger;

    public EvolutionClient(HttpClient http, WhatsAppConfig config, ILogger<EvolutionClient> logger)
    {
        _http   = http;
        _config = config;
        _logger = logger;
    }

    private string Instancia => Uri.EscapeDataString(_config.Instancia);

    public async Task<EvolutionResultado<bool>> CrearInstanciaSiNoExisteAsync(CancellationToken ct = default)
    {
        var existente = await ObtenerInstanciaAsync(ct);
        if (existente.Exito) return EvolutionResultado<bool>.Ok(false);
        if (existente.CodigoError != EvolutionCodigos.NoEncontrado)
            return EvolutionResultado<bool>.Error(existente.CodigoError!, existente.Mensaje!);

        var r = await EnviarAsync(HttpMethod.Post, "instance/create", new
        {
            instanceName = _config.Instancia,
            integration  = "WHATSAPP-BAILEYS",
            qrcode       = true
        }, ct);
        return r.Exito ? EvolutionResultado<bool>.Ok(true) : Convertir<bool>(r);
    }

    public async Task<EvolutionResultado<string>> ObtenerEstadoAsync(CancellationToken ct = default)
    {
        var r = await EnviarAsync(HttpMethod.Get, $"instance/connectionState/{Instancia}", null, ct);
        if (!r.Exito) return Convertir<string>(r);

        // { instance: { instanceName, state } }  (algunas versiones: { state })
        var raiz  = r.Valor;
        var state = Texto(raiz, "instance", "state") ?? Texto(raiz, "state");
        return state is null
            ? EvolutionResultado<string>.Error(EvolutionCodigos.Http, "Respuesta de estado sin 'state'.")
            : EvolutionResultado<string>.Ok(state);
    }

    public async Task<EvolutionResultado<EvolutionInstancia>> ObtenerInstanciaAsync(CancellationToken ct = default)
    {
        var r = await EnviarAsync(HttpMethod.Get, $"instance/fetchInstances?instanceName={Instancia}", null, ct);
        if (!r.Exito) return Convertir<EvolutionInstancia>(r);

        if (r.Valor.ValueKind != JsonValueKind.Array || r.Valor.GetArrayLength() == 0)
            return EvolutionResultado<EvolutionInstancia>.Error(EvolutionCodigos.NoEncontrado, "La instancia no existe.");

        var item = r.Valor[0];
        // v2.1+: { name, connectionStatus, ownerJid, profileName, number }
        // v2.0 : { instance: { instanceName, status, owner, profileName } }
        var estado = Texto(item, "connectionStatus") ?? Texto(item, "instance", "status");
        var owner  = Texto(item, "number") ?? Texto(item, "ownerJid") ?? Texto(item, "instance", "owner");
        var perfil = Texto(item, "profileName") ?? Texto(item, "instance", "profileName");
        var numero = owner?.Split('@')[0].Split(':')[0];
        return EvolutionResultado<EvolutionInstancia>.Ok(new EvolutionInstancia(estado, numero, perfil));
    }

    public async Task<EvolutionResultado<EvolutionQr>> ConectarAsync(CancellationToken ct = default)
    {
        var r = await EnviarAsync(HttpMethod.Get, $"instance/connect/{Instancia}", null, ct);
        if (!r.Exito) return Convertir<EvolutionQr>(r);

        var base64 = Texto(r.Valor, "base64") ?? Texto(r.Valor, "qrcode", "base64");
        var code   = Texto(r.Valor, "pairingCode") ?? Texto(r.Valor, "qrcode", "pairingCode");
        if (base64 is not null && base64.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            base64 = base64[(base64.IndexOf(',') + 1)..];
        return EvolutionResultado<EvolutionQr>.Ok(new EvolutionQr(base64, code));
    }

    public async Task<EvolutionResultado<bool>> DesconectarAsync(CancellationToken ct = default)
    {
        var r = await EnviarAsync(HttpMethod.Delete, $"instance/logout/{Instancia}", null, ct);
        return r.Exito ? EvolutionResultado<bool>.Ok(true) : Convertir<bool>(r);
    }

    public async Task<EvolutionResultado<bool>> ReiniciarInstanciaAsync(CancellationToken ct = default)
    {
        // ⚠️ NO VERIFICADO contra la imagen fijada de Evolution v2: método y ruta tomados del plan
        // (POST instance/restart/{instancia}). Si esa versión no lo tiene, el alternativo es
        // GET instance/connect/{instancia}, que con una sesión guardada reconecta sin QR.
        var r = await EnviarAsync(HttpMethod.Post, $"instance/restart/{Instancia}", null, ct);
        return r.Exito ? EvolutionResultado<bool>.Ok(true) : Convertir<bool>(r);
    }

    public async Task<EvolutionResultado<bool>> ConfigurarWebhookAsync(string url, string token, CancellationToken ct = default)
    {
        // El token va en la cabecera y, como respaldo (versiones sin cabeceras custom), en el query string.
        var separador = url.Contains('?') ? '&' : '?';
        var urlConToken = $"{url}{separador}token={Uri.EscapeDataString(token)}";

        var r = await EnviarAsync(HttpMethod.Post, $"webhook/set/{Instancia}", new
        {
            webhook = new
            {
                enabled  = true,
                url      = urlConToken,
                headers  = new Dictionary<string, string> { ["x-webhook-token"] = token },
                byEvents = false,
                base64   = false,
                events   = new[] { "CONNECTION_UPDATE" }
            }
        }, ct);
        return r.Exito ? EvolutionResultado<bool>.Ok(true) : Convertir<bool>(r);
    }

    public async Task<EvolutionResultado<bool>> VerificarNumeroAsync(string numero, CancellationToken ct = default)
    {
        var r = await EnviarAsync(HttpMethod.Post, $"chat/whatsappNumbers/{Instancia}",
            new { numbers = new[] { numero } }, ct);
        if (!r.Exito) return Convertir<bool>(r);

        if (r.Valor.ValueKind == JsonValueKind.Array && r.Valor.GetArrayLength() > 0
            && r.Valor[0].TryGetProperty("exists", out var exists)
            && (exists.ValueKind == JsonValueKind.True || exists.ValueKind == JsonValueKind.False))
            return EvolutionResultado<bool>.Ok(exists.GetBoolean());

        return EvolutionResultado<bool>.Error(EvolutionCodigos.Http, "Respuesta de verificación inesperada.");
    }

    public async Task<EvolutionResultado<bool>> EnviarTextoAsync(string numero, string texto, CancellationToken ct = default)
    {
        var r = await EnviarAsync(HttpMethod.Post, $"message/sendText/{Instancia}",
            new { number = numero, text = texto }, ct);
        return r.Exito ? EvolutionResultado<bool>.Ok(true) : Convertir<bool>(r);
    }

    // ── Helpers ───────────────────────────────────────────────────

    private async Task<EvolutionResultado<JsonElement>> EnviarAsync(
        HttpMethod metodo, string ruta, object? cuerpo, CancellationToken ct)
    {
        if (!_config.Configurado)
            return EvolutionResultado<JsonElement>.Error(EvolutionCodigos.NoConfigurado, "WhatsApp no está configurado.");

        try
        {
            using var req = new HttpRequestMessage(metodo, ruta);
            if (cuerpo is not null) req.Content = JsonContent.Create(cuerpo);

            using var resp = await _http.SendAsync(req, ct);
            var contenido  = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                // Solo el código: el cuerpo podría traer datos de la instancia.
                _logger.LogWarning("Evolution {Metodo} {Ruta} → HTTP {Codigo}",
                    metodo.Method, RutaSinQuery(ruta), (int)resp.StatusCode);
                var codigo = resp.StatusCode == HttpStatusCode.NotFound
                    ? EvolutionCodigos.NoEncontrado : EvolutionCodigos.Http;
                return EvolutionResultado<JsonElement>.Error(codigo, $"Evolution respondió HTTP {(int)resp.StatusCode}.");
            }

            if (string.IsNullOrWhiteSpace(contenido))
                return EvolutionResultado<JsonElement>.Ok(default);

            using var doc = JsonDocument.Parse(contenido);
            return EvolutionResultado<JsonElement>.Ok(doc.RootElement.Clone());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            if (ct.IsCancellationRequested) throw;
            _logger.LogWarning("Evolution {Metodo} {Ruta} inalcanzable: {Tipo}",
                metodo.Method, RutaSinQuery(ruta), ex.GetType().Name);
            return EvolutionResultado<JsonElement>.Error(EvolutionCodigos.Inalcanzable, "El servicio de WhatsApp no responde.");
        }
        catch (JsonException)
        {
            _logger.LogWarning("Evolution {Metodo} {Ruta}: respuesta no es JSON", metodo.Method, RutaSinQuery(ruta));
            return EvolutionResultado<JsonElement>.Error(EvolutionCodigos.Http, "Respuesta inválida del servicio de WhatsApp.");
        }
    }

    private static EvolutionResultado<T> Convertir<T>(EvolutionResultado<JsonElement> r) =>
        EvolutionResultado<T>.Error(r.CodigoError ?? EvolutionCodigos.Http, r.Mensaje ?? "Error de Evolution.");

    private static string RutaSinQuery(string ruta) => ruta.Split('?')[0];

    private static string? Texto(JsonElement elemento, params string[] ruta)
    {
        if (elemento.ValueKind != JsonValueKind.Object) return null;
        var actual = elemento;
        foreach (var parte in ruta)
        {
            if (actual.ValueKind != JsonValueKind.Object || !actual.TryGetProperty(parte, out actual))
                return null;
        }
        return actual.ValueKind == JsonValueKind.String ? actual.GetString() : null;
    }
}
