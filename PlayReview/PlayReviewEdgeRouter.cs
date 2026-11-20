using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Yarp.ReverseProxy.Forwarder;

namespace FloreriaBautista.PlayReview;

/// <summary>
/// Enrutador de borde de PROD. Es el PRIMER middleware de la cadena (antes de Datadog,
/// Exception, RequestLogging, Rasp, Alexa y Authentication): si la petición es de una
/// cuenta de revisión la reenvía tal cual a REVIEW con YARP y termina, sin que PROD
/// la autentique, la registre con datos ni toque su base. Si no lo es, sigue el
/// pipeline normal sin ningún cambio.
///
/// Se decide solo con datos que emite el servidor (nunca con un header del cliente):
///  1. Login/registro/recuperación con un correo que termina en alguno de los dominios
///     de revisión (<c>REVIEW_LOGIN_EMAIL_DOMAIN</c>, lista separada por comas).
///  2. JWT (Authorization Bearer, o <c>access_token</c> en <c>/hubs</c>) cuyo <c>iss</c>
///     es el de REVIEW. Solo se decodifica el payload; la firma la valida REVIEW, así que
///     un token falsificado con ese <c>iss</c> termina rechazado allá, nunca aceptado aquí.
///  3. Refresh/logout con un refresh token opaco que empieza con <c>rv_</c>.
/// </summary>
public class PlayReviewEdgeRouter
{
    /// <summary>Tope de cuerpo que se inspecciona; un login legítimo pesa unos cientos de bytes.</summary>
    internal const int MaxBytesCuerpo = 16 * 1024;

    private static readonly HashSet<string> RutasPorCorreo = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/login",
        "/api/auth/register",
        "/api/auth/password/forgot",
    };

    private static readonly HashSet<string> RutasPorRefresh = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/token/refresh",
        "/api/auth/logout",
    };

    private readonly RequestDelegate               _next;
    private readonly OpcionesBordeRevision         _opciones;
    private readonly IHttpForwarder                _forwarder;
    private readonly HttpMessageInvoker            _cliente;
    private readonly ILogger<PlayReviewEdgeRouter> _logger;
    private readonly ForwarderRequestConfig        _config = new() { ActivityTimeout = TimeSpan.FromSeconds(100) };

    public PlayReviewEdgeRouter(
        RequestDelegate next,
        OpcionesBordeRevision opciones,
        IHttpForwarder forwarder,
        ClienteBordeRevision cliente,
        ILogger<PlayReviewEdgeRouter> logger)
    {
        _next      = next;
        _opciones  = opciones;
        _forwarder = forwarder;
        _cliente   = cliente.Invoker;
        _logger    = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (!_opciones.Activo || !await EsPeticionDeRevisionAsync(ctx))
        {
            await _next(ctx);
            return;
        }

        // El header de firma que mande el cliente se descarta siempre; el transformador
        // agrega el bueno sobre la petición saliente.
        ctx.Request.Headers.Remove(PlayReviewEntorno.HeaderFirma);

        var sw    = Stopwatch.StartNew();
        var error = await _forwarder.SendAsync(
            ctx, _opciones.UpstreamUrl, _cliente, _config, new TransformadorBorde(_opciones.Secreto),
            ctx.RequestAborted);
        sw.Stop();

        // Sin query, sin cuerpo, sin IP: solo lo necesario para operar.
        if (error == ForwarderError.None)
        {
            _logger.LogInformation("[play_review-borde] {Method} {Path} reenviada → {Status} | {Ms} ms",
                ctx.Request.Method, ctx.Request.Path, ctx.Response.StatusCode, sw.ElapsedMilliseconds);
            return;
        }

        _logger.LogWarning("[play_review-borde] {Method} {Path} no se pudo reenviar: {Error}",
            ctx.Request.Method, ctx.Request.Path, error);

        if (!ctx.Response.HasStarted)
        {
            ctx.Response.Clear();
            ctx.Response.StatusCode  = (int)HttpStatusCode.BadGateway;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                success = false,
                message = "El entorno de revisión no está disponible en este momento. Intenta de nuevo en un minuto. / " +
                          "The review environment is not available right now. Please retry in a minute."
            }));
        }
    }

    /// <summary>¿La petición pertenece al entorno de revisión? Público para las pruebas.</summary>
    public async Task<bool> EsPeticionDeRevisionAsync(HttpContext ctx)
    {
        var req = ctx.Request;

        // 1. Bearer en Authorization.
        var auth = req.Headers.Authorization.ToString();
        if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            && TieneIssuerDeRevision(auth["Bearer ".Length..].Trim()))
            return true;

        // 2. SignalR: el WebSocket no puede mandar headers y el token viaja en el query.
        if (req.Path.StartsWithSegments("/hubs"))
        {
            var qs = req.Query["access_token"].ToString();
            if (!string.IsNullOrEmpty(qs) && TieneIssuerDeRevision(qs)) return true;
        }

        // 3. Cuerpos de autenticación (solo POST a rutas concretas).
        if (!HttpMethods.IsPost(req.Method)) return false;

        var ruta = req.Path.Value ?? string.Empty;
        var porCorreo  = RutasPorCorreo.Contains(ruta);
        var porRefresh = RutasPorRefresh.Contains(ruta);
        if (!porCorreo && !porRefresh) return false;

        using var json = await LeerCuerpoJsonAsync(req);
        if (json is null || json.RootElement.ValueKind != JsonValueKind.Object) return false;

        if (porCorreo)
        {
            var correo = Propiedad(json.RootElement, "correo");
            return PlayReviewEntorno.CorreoEsDeRevision(correo, _opciones.DominiosLogin);
        }

        var refresh = Propiedad(json.RootElement, "refreshToken");
        return refresh is not null && refresh.StartsWith(PlayReviewEntorno.PrefijoRefresh, StringComparison.Ordinal);
    }

    /// <summary>
    /// Lee el <c>iss</c> del payload SIN validar la firma. Cualquier cosa mal formada
    /// devuelve false y la petición sigue el camino normal de PROD.
    /// </summary>
    internal bool TieneIssuerDeRevision(string token)
    {
        try
        {
            var partes = token.Split('.');
            if (partes.Length != 3 || partes[1].Length == 0 || partes[1].Length > 8192) return false;

            using var payload = JsonDocument.Parse(FirmaBorde.DesdeBase64Url(partes[1]));
            return payload.RootElement.ValueKind == JsonValueKind.Object
                && payload.RootElement.TryGetProperty("iss", out var iss)
                && iss.ValueKind == JsonValueKind.String
                && string.Equals(iss.GetString(), _opciones.IssuerRevision, StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Lee el cuerpo con buffering y lo rebobina, para que lo vuelva a leer quien siga
    /// (YARP al reenviar o el controlador de PROD). Si pasa del tope, no se considera
    /// de revisión y PROD lo maneja como siempre.
    /// </summary>
    private static async Task<JsonDocument?> LeerCuerpoJsonAsync(HttpRequest req)
    {
        if (req.ContentLength is > MaxBytesCuerpo) return null;
        if (!string.IsNullOrEmpty(req.ContentType)
            && !req.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase))
            return null;

        req.EnableBuffering();
        try
        {
            var buffer = new byte[MaxBytesCuerpo + 1];
            var total  = 0;
            int leidos;
            while (total < buffer.Length
                   && (leidos = await req.Body.ReadAsync(buffer.AsMemory(total, buffer.Length - total))) > 0)
                total += leidos;

            if (total == 0 || total > MaxBytesCuerpo) return null;
            return JsonDocument.Parse(buffer.AsMemory(0, total));
        }
        catch (JsonException)
        {
            return null;
        }
        finally
        {
            req.Body.Position = 0;
        }
    }

    private static string? Propiedad(JsonElement objeto, string nombre)
    {
        foreach (var p in objeto.EnumerateObject())
            if (string.Equals(p.Name, nombre, StringComparison.OrdinalIgnoreCase))
                return p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : null;
        return null;
    }
}

/// <summary>
/// Copia la petición como lo hace YARP por defecto (conserva los headers originales,
/// incluido <c>X-Forwarded-For</c>, y usa el Host del destino) y agrega la firma del borde.
/// </summary>
public sealed class TransformadorBorde : HttpTransformer
{
    private readonly string _secreto;
    public TransformadorBorde(string secreto) => _secreto = secreto;

    public override async ValueTask TransformRequestAsync(
        HttpContext httpContext, HttpRequestMessage proxyRequest, string destinationPrefix,
        CancellationToken cancellationToken)
    {
        await base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix, cancellationToken);

        // Headers que ponen Cloudflare/Render al entrar a PROD. Si viajan al servicio REVIEW
        // (que también está detrás de Cloudflare/Render), el CDN lo toma como bucle y responde 508.
        QuitarHeadersDeInfraestructura(proxyRequest);

        var ruta = (httpContext.Request.PathBase + httpContext.Request.Path).Value ?? "/";
        proxyRequest.Headers.Remove(PlayReviewEntorno.HeaderFirma);
        proxyRequest.Headers.TryAddWithoutValidation(
            PlayReviewEntorno.HeaderFirma,
            FirmaBorde.CrearHeader(_secreto, DateTimeOffset.UtcNow, proxyRequest.Method.Method, ruta));
    }

    private static readonly string[] PrefijosInfraestructura = ["cf-", "cdn-loop", "rndr-", "render-", "true-client-ip", "x-request-start"];

    public static void QuitarHeadersDeInfraestructura(HttpRequestMessage proxyRequest)
    {
        var quitar = proxyRequest.Headers
            .Select(h => h.Key)
            .Where(k => PrefijosInfraestructura.Any(p => k.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        foreach (var k in quitar)
            proxyRequest.Headers.Remove(k);
    }
}

/// <summary>Cliente HTTP dedicado al reenvío (sin proxy del sistema, cookies ni redirecciones).</summary>
public sealed class ClienteBordeRevision : IDisposable
{
    public HttpMessageInvoker Invoker { get; }

    public ClienteBordeRevision() : this(new HttpMessageInvoker(new SocketsHttpHandler
    {
        UseProxy                  = false,
        AllowAutoRedirect         = false,
        AutomaticDecompression    = DecompressionMethods.None,
        UseCookies                = false,
        EnableMultipleHttp2Connections = true,
        ActivityHeadersPropagator = null,
        ConnectTimeout            = TimeSpan.FromSeconds(15),
    })) { }

    public ClienteBordeRevision(HttpMessageInvoker invoker) => Invoker = invoker;

    public void Dispose() => Invoker.Dispose();
}
