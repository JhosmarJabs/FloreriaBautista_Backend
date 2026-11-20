using System.Text;
using FloreriaBautista.PlayReview;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Yarp.ReverseProxy.Forwarder;

namespace FloreriaBautista.Tests;

/// <summary>
/// Enrutador de borde de PROD (play_review). Sin red: el reenvío se sustituye por un
/// IHttpForwarder falso que solo registra la llamada.
/// </summary>
public class PlayReviewBordeTests
{
    private const string Secreto   = "secreto-de-prueba-de-al-menos-32-caracteres-xx";
    private const string IssRev    = "floreria-play-review";
    private const string IssProd   = "floreria-prod";
    private const string Upstream  = "https://floreria-review.example.test";

    private sealed class ForwarderFalso : IHttpForwarder
    {
        public int Llamadas { get; private set; }
        public string? Destino { get; private set; }
        public HttpTransformer? Transformador { get; private set; }
        public string? CuerpoVisto { get; private set; }

        public ValueTask<ForwarderError> SendAsync(HttpContext context, string destinationPrefix,
            HttpMessageInvoker httpClient, ForwarderRequestConfig requestConfig, HttpTransformer transformer)
            => SendAsync(context, destinationPrefix, httpClient, requestConfig, transformer, CancellationToken.None);

        public async ValueTask<ForwarderError> SendAsync(HttpContext context, string destinationPrefix,
            HttpMessageInvoker httpClient, ForwarderRequestConfig requestConfig, HttpTransformer transformer,
            CancellationToken cancellationToken)
        {
            Llamadas++;
            Destino       = destinationPrefix;
            Transformador = transformer;
            // YARP leería el cuerpo desde el inicio: el router debe haberlo rebobinado.
            using var lector = new StreamReader(context.Request.Body, leaveOpen: true);
            CuerpoVisto = await lector.ReadToEndAsync(cancellationToken);
            context.Response.StatusCode = 200;
            return ForwarderError.None;
        }
    }

    private static Func<string, string?> Entorno(Dictionary<string, string?> vars) =>
        k => vars.TryGetValue(k, out var v) ? v : null;

    private static OpcionesBordeRevision OpcionesActivas() => OpcionesBordeRevision.Desde(Entorno(new()
    {
        ["REVIEW_UPSTREAM_URL"] = Upstream,
        ["REVIEW_EDGE_SECRET"]  = Secreto,
        ["REVIEW_JWT_ISSUER"]   = IssRev,
        ["JWT_ISSUER"]          = IssProd,
    }));

    private static (PlayReviewEdgeRouter Router, ForwarderFalso Fwd, Func<bool> SiguioNormal, Func<string?> CuerpoEnProd)
        Crear(OpcionesBordeRevision opciones)
    {
        var fwd = new ForwarderFalso();
        var siguio = false;
        string? cuerpo = null;
        RequestDelegate next = async ctx =>
        {
            siguio = true;
            using var lector = new StreamReader(ctx.Request.Body, leaveOpen: true);
            cuerpo = await lector.ReadToEndAsync();
        };
        var router = new PlayReviewEdgeRouter(next, opciones, fwd,
            new ClienteBordeRevision(new HttpMessageInvoker(new HttpClientHandler())),
            NullLogger<PlayReviewEdgeRouter>.Instance);
        return (router, fwd, () => siguio, () => cuerpo);
    }

    private static DefaultHttpContext Peticion(string metodo, string ruta, string? json = null,
        Dictionary<string, string>? headers = null, string? query = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = metodo;
        ctx.Request.Path   = ruta;
        if (query is not null) ctx.Request.QueryString = new QueryString(query);
        if (json is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            ctx.Request.Body          = new MemoryStream(bytes);
            ctx.Request.ContentLength = bytes.Length;
            ctx.Request.ContentType   = "application/json";
        }
        foreach (var (k, v) in headers ?? []) ctx.Request.Headers[k] = v;
        ctx.Response.Body = new MemoryStream();
        return ctx;
    }

    /// <summary>JWT sin firma válida: el borde solo mira el payload.</summary>
    private static string Jwt(string iss)
    {
        static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{B64("{\"alg\":\"HS256\",\"typ\":\"JWT\"}")}.{B64($"{{\"sub\":\"x\",\"iss\":\"{iss}\"}}")}.firma-que-no-se-valida";
    }

    // ── Activación ───────────────────────────────────────────────

    [Fact]
    public async Task Sin_Variables_El_Router_Esta_Inactivo_Y_No_Hace_Nada()
    {
        var opciones = OpcionesBordeRevision.Desde(_ => null);
        Assert.False(opciones.Activo);

        var (router, fwd, siguio, _) = Crear(opciones);
        var ctx = Peticion("POST", "/api/auth/login", "{\"correo\":\"revisor.admin@playreview.invalid\",\"contrasena\":\"x\"}");
        await router.InvokeAsync(ctx);

        Assert.True(siguio());
        Assert.Equal(0, fwd.Llamadas);
    }

    [Theory]
    [InlineData("REVIEW_UPSTREAM_URL")]
    [InlineData("REVIEW_EDGE_SECRET")]
    [InlineData("REVIEW_JWT_ISSUER")]
    public void Falta_Una_Variable_Imprescindible_Queda_Inactivo(string faltante)
    {
        var vars = new Dictionary<string, string?>
        {
            ["REVIEW_UPSTREAM_URL"] = Upstream, ["REVIEW_EDGE_SECRET"] = Secreto,
            ["REVIEW_JWT_ISSUER"] = IssRev, ["JWT_ISSUER"] = IssProd,
        };
        vars.Remove(faltante);
        Assert.False(OpcionesBordeRevision.Desde(Entorno(vars)).Activo);
    }

    [Fact]
    public void Issuer_De_Revision_Igual_Al_De_Prod_Desactiva_El_Router()
    {
        var o = OpcionesBordeRevision.Desde(Entorno(new()
        {
            ["REVIEW_UPSTREAM_URL"] = Upstream, ["REVIEW_EDGE_SECRET"] = Secreto,
            ["REVIEW_JWT_ISSUER"] = IssProd, ["JWT_ISSUER"] = IssProd,
        }));
        Assert.False(o.Activo);
    }

    [Theory]
    [InlineData("corto")]
    [InlineData("http://floreria-review.example.test")]
    [InlineData("https://floreria-review.example.test/api")]
    public void Secreto_Corto_O_Url_Insegura_Desactiva_El_Router(string caso)
    {
        var vars = new Dictionary<string, string?>
        {
            ["REVIEW_UPSTREAM_URL"] = Upstream, ["REVIEW_EDGE_SECRET"] = Secreto,
            ["REVIEW_JWT_ISSUER"] = IssRev, ["JWT_ISSUER"] = IssProd,
        };
        if (caso == "corto") vars["REVIEW_EDGE_SECRET"] = "corto";
        else vars["REVIEW_UPSTREAM_URL"] = caso;
        Assert.False(OpcionesBordeRevision.Desde(Entorno(vars)).Activo);
    }

    // ── Dominios de login (REVIEW_LOGIN_EMAIL_DOMAIN, lista separada por comas) ──

    private static OpcionesBordeRevision OpcionesConDominios(string? dominios) => OpcionesBordeRevision.Desde(Entorno(new()
    {
        ["REVIEW_UPSTREAM_URL"]       = Upstream,
        ["REVIEW_EDGE_SECRET"]        = Secreto,
        ["REVIEW_JWT_ISSUER"]         = IssRev,
        ["JWT_ISSUER"]                = IssProd,
        ["REVIEW_LOGIN_EMAIL_DOMAIN"] = dominios,
    }));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Sin_Dominio_Se_Usa_El_Por_Defecto(string? valor)
    {
        var o = OpcionesConDominios(valor);
        Assert.True(o.Activo);
        Assert.Equal(["@playreview.invalid"], o.DominiosLogin);
    }

    [Fact]
    public void Varios_Dominios_Se_Normalizan_Y_Sin_Duplicados()
    {
        var o = OpcionesConDominios(" PlayReview.invalid , @TEST.com,, @test.com ");
        Assert.True(o.Activo);
        Assert.Equal(["@playreview.invalid", "@test.com"], o.DominiosLogin);
    }

    [Theory]
    [InlineData("@playreview.invalid,gmail")]
    [InlineData("@,@test.com")]
    [InlineData("@test.com,@.com")]
    [InlineData("@test.com,test.")]
    [InlineData("@test.com,a@b@c.com")]
    [InlineData("@test.com,mi dominio.com")]
    public void Un_Dominio_Invalido_En_La_Lista_Desactiva_El_Router(string valor)
    {
        var o = OpcionesConDominios(valor);
        Assert.False(o.Activo);
        Assert.Contains("REVIEW_LOGIN_EMAIL_DOMAIN", o.MotivoInactivo);
    }

    [Theory]
    [InlineData("revisor.admin@playreview.invalid")]
    [InlineData("liz.prueba@test.com")]
    [InlineData("  Gadiel.Admin@TEST.COM ")]
    public async Task Con_Varios_Dominios_Cualquiera_Se_Reenvia(string correo)
    {
        var (router, fwd, siguio, _) = Crear(OpcionesConDominios("@playreview.invalid,@test.com"));
        var json = $"{{\"correo\":\"{correo}\",\"contrasena\":\"no-importa\"}}";

        await router.InvokeAsync(Peticion("POST", "/api/auth/login", json));

        Assert.False(siguio());
        Assert.Equal(1, fwd.Llamadas);
        Assert.Equal(json, fwd.CuerpoVisto);
    }

    [Theory]
    [InlineData("x@nottest.com")]
    [InlineData("x@sub.test.com")]
    [InlineData("x@test.com.atacante.com")]
    [InlineData("cliente@gmail.com")]
    public async Task Con_Varios_Dominios_Lo_Demas_Sigue_En_Prod(string correo)
    {
        var (router, fwd, siguio, _) = Crear(OpcionesConDominios("@playreview.invalid,@test.com"));
        await router.InvokeAsync(Peticion("POST", "/api/auth/login", $"{{\"correo\":\"{correo}\",\"contrasena\":\"x\"}}"));
        Assert.True(siguio());
        Assert.Equal(0, fwd.Llamadas);
    }

    [Fact]
    public async Task Sin_Test_Com_En_La_Lista_Un_Tester_Se_Queda_En_Prod()
    {
        var (router, fwd, siguio, _) = Crear(OpcionesActivas());   // solo el dominio por defecto
        await router.InvokeAsync(Peticion("POST", "/api/auth/register",
            "{\"correo\":\"liz.prueba@test.com\",\"contrasena\":\"x\"}"));
        Assert.True(siguio());
        Assert.Equal(0, fwd.Llamadas);
    }

    [Fact]
    public void La_Instancia_De_Revision_Nunca_Actua_Como_Borde()
    {
        var o = OpcionesBordeRevision.Desde(Entorno(new()
        {
            ["APP_ENTORNO"] = "play_review",
            ["REVIEW_UPSTREAM_URL"] = Upstream, ["REVIEW_EDGE_SECRET"] = Secreto,
            ["REVIEW_JWT_ISSUER"] = IssRev, ["JWT_ISSUER"] = IssProd,
        }));
        Assert.False(o.Activo);
    }

    // ── Login ────────────────────────────────────────────────────

    [Theory]
    [InlineData("revisor.admin@playreview.invalid")]
    [InlineData("  Revisor.Empleado@PLAYREVIEW.INVALID ")]
    public async Task Login_Con_Dominio_De_Revision_Se_Reenvia_Con_El_Cuerpo_Intacto(string correo)
    {
        var (router, fwd, siguio, _) = Crear(OpcionesActivas());
        var json = $"{{\"correo\":\"{correo}\",\"contrasena\":\"no-importa\"}}";
        var ctx = Peticion("POST", "/api/auth/login", json);

        await router.InvokeAsync(ctx);

        Assert.False(siguio());
        Assert.Equal(1, fwd.Llamadas);
        Assert.Equal(Upstream, fwd.Destino);
        Assert.Equal(json, fwd.CuerpoVisto);
    }

    [Fact]
    public async Task Login_Normal_Sigue_En_Prod_Y_El_Controlador_Puede_Leer_El_Cuerpo()
    {
        var (router, fwd, siguio, cuerpo) = Crear(OpcionesActivas());
        var json = "{\"correo\":\"cliente@gmail.com\",\"contrasena\":\"x\"}";

        await router.InvokeAsync(Peticion("POST", "/api/auth/login", json));

        Assert.True(siguio());
        Assert.Equal(0, fwd.Llamadas);
        Assert.Equal(json, cuerpo());
    }

    [Fact]
    public async Task Correo_Que_Solo_Contiene_El_Dominio_No_Es_De_Revision()
    {
        var (router, fwd, siguio, _) = Crear(OpcionesActivas());
        await router.InvokeAsync(Peticion("POST", "/api/auth/login",
            "{\"correo\":\"x@playreview.invalid.atacante.com\",\"contrasena\":\"x\"}"));
        Assert.True(siguio());
        Assert.Equal(0, fwd.Llamadas);
    }

    [Fact]
    public async Task Cuerpo_Mal_Formado_Sigue_En_Prod()
    {
        var (router, fwd, siguio, _) = Crear(OpcionesActivas());
        await router.InvokeAsync(Peticion("POST", "/api/auth/login", "{correo: playreview.invalid"));
        Assert.True(siguio());
        Assert.Equal(0, fwd.Llamadas);
    }

    // ── Token ────────────────────────────────────────────────────

    [Fact]
    public async Task Bearer_Con_Iss_De_Revision_Se_Reenvia()
    {
        var (router, fwd, siguio, _) = Crear(OpcionesActivas());
        await router.InvokeAsync(Peticion("GET", "/api/admin/orders",
            headers: new() { ["Authorization"] = "Bearer " + Jwt(IssRev) }));
        Assert.False(siguio());
        Assert.Equal(1, fwd.Llamadas);
    }

    [Fact]
    public async Task Bearer_Con_Iss_De_Prod_Sigue_En_Prod()
    {
        var (router, fwd, siguio, _) = Crear(OpcionesActivas());
        await router.InvokeAsync(Peticion("GET", "/api/admin/orders",
            headers: new() { ["Authorization"] = "Bearer " + Jwt(IssProd) }));
        Assert.True(siguio());
        Assert.Equal(0, fwd.Llamadas);
    }

    [Theory]
    [InlineData("no-es-un-jwt")]
    [InlineData("a.b.c")]
    [InlineData("a.%%%.c")]
    [InlineData("")]
    public async Task Token_Mal_Formado_Sigue_En_Prod(string token)
    {
        var (router, fwd, siguio, _) = Crear(OpcionesActivas());
        await router.InvokeAsync(Peticion("GET", "/api/admin/orders",
            headers: new() { ["Authorization"] = "Bearer " + token }));
        Assert.True(siguio());
        Assert.Equal(0, fwd.Llamadas);
    }

    [Fact]
    public async Task SignalR_Con_Access_Token_De_Revision_En_Query_Se_Reenvia()
    {
        var (router, fwd, siguio, _) = Crear(OpcionesActivas());
        await router.InvokeAsync(Peticion("GET", "/hubs/venta-instantanea", query: "?access_token=" + Jwt(IssRev)));
        Assert.False(siguio());
        Assert.Equal(1, fwd.Llamadas);
    }

    [Fact]
    public async Task Access_Token_En_Query_Fuera_De_Hubs_No_Cuenta()
    {
        var (router, fwd, siguio, _) = Crear(OpcionesActivas());
        await router.InvokeAsync(Peticion("GET", "/api/admin/orders", query: "?access_token=" + Jwt(IssRev)));
        Assert.True(siguio());
        Assert.Equal(0, fwd.Llamadas);
    }

    // ── Refresh opaco ────────────────────────────────────────────

    [Fact]
    public async Task Refresh_Con_Prefijo_rv_Se_Reenvia_Y_Uno_Normal_No()
    {
        var (router, fwd, siguio, _) = Crear(OpcionesActivas());
        await router.InvokeAsync(Peticion("POST", "/api/auth/token/refresh", "{\"refreshToken\":\"rv_abc123\"}"));
        Assert.Equal(1, fwd.Llamadas);
        Assert.False(siguio());

        var (router2, fwd2, siguio2, _) = Crear(OpcionesActivas());
        await router2.InvokeAsync(Peticion("POST", "/api/auth/token/refresh", "{\"refreshToken\":\"QUJDREVG+/==\"}"));
        Assert.Equal(0, fwd2.Llamadas);
        Assert.True(siguio2());
    }

    [Fact]
    public void Refresh_De_Revision_Lleva_Prefijo_Y_El_De_Prod_Nunca_Puede_Tenerlo()
    {
        var revision = Services.Auth.AuthService.GenerarValorRefresh(modoRevision: true);
        var prod     = Services.Auth.AuthService.GenerarValorRefresh(modoRevision: false);

        Assert.StartsWith(PlayReviewEntorno.PrefijoRefresh, revision);
        Assert.True(revision.Length <= 512);   // cabe en auth_tokens.token
        // Base64 no usa '_': un token de PROD jamás empieza con "rv_".
        Assert.DoesNotContain('_', prod);
    }

    // ── Headers del cliente ──────────────────────────────────────

    [Fact]
    public async Task Headers_Del_Cliente_Para_Elegir_Entorno_Se_Ignoran()
    {
        var (router, fwd, siguio, _) = Crear(OpcionesActivas());
        var ctx = Peticion("POST", "/api/auth/login", "{\"correo\":\"cliente@gmail.com\",\"contrasena\":\"x\"}",
            new()
            {
                ["X-Env"] = "play_review",
                ["X-Entorno"] = "play_review",
                [PlayReviewEntorno.HeaderFirma] = FirmaBorde.CrearHeader(Secreto, DateTimeOffset.UtcNow, "POST", "/api/auth/login"),
            });

        await router.InvokeAsync(ctx);

        Assert.True(siguio());
        Assert.Equal(0, fwd.Llamadas);
    }

    [Fact]
    public async Task Al_Reenviar_Se_Descarta_La_Firma_Del_Cliente_Y_Se_Pone_Una_Valida()
    {
        var (router, fwd, _, _) = Crear(OpcionesActivas());
        var ctx = Peticion("GET", "/api/employee/orders",
            headers: new()
            {
                ["Authorization"] = "Bearer " + Jwt(IssRev),
                [PlayReviewEntorno.HeaderFirma] = "123.firma-falsa-del-cliente",
            });

        await router.InvokeAsync(ctx);

        Assert.False(ctx.Request.Headers.ContainsKey(PlayReviewEntorno.HeaderFirma));
        Assert.IsType<TransformadorBorde>(fwd.Transformador);

        var saliente = new HttpRequestMessage(HttpMethod.Get, Upstream + "/api/employee/orders");
        saliente.Headers.TryAddWithoutValidation(PlayReviewEntorno.HeaderFirma, "otra-falsa");
        await fwd.Transformador!.TransformRequestAsync(ctx, saliente, Upstream, CancellationToken.None);

        var firma = Assert.Single(saliente.Headers.GetValues(PlayReviewEntorno.HeaderFirma));
        Assert.Equal(ResultadoFirma.Valida,
            FirmaBorde.Validar(Secreto, firma, "GET", "/api/employee/orders", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Reenvio_quita_headers_de_Cloudflare_y_Render_para_evitar_508()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "https://review.example/api/auth/login");
        req.Headers.TryAddWithoutValidation("CDN-Loop", "cloudflare");
        req.Headers.TryAddWithoutValidation("CF-Ray", "abc");
        req.Headers.TryAddWithoutValidation("CF-Connecting-IP", "1.2.3.4");
        req.Headers.TryAddWithoutValidation("Rndr-Id", "x");
        req.Headers.TryAddWithoutValidation("True-Client-IP", "1.2.3.4");
        req.Headers.TryAddWithoutValidation("X-Forwarded-For", "1.2.3.4");
        req.Headers.TryAddWithoutValidation("Authorization", "Bearer t");

        TransformadorBorde.QuitarHeadersDeInfraestructura(req);

        var claves = req.Headers.Select(h => h.Key).ToList();
        Assert.DoesNotContain(claves, k => k.StartsWith("CF-", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("CDN-Loop", claves);
        Assert.DoesNotContain("Rndr-Id", claves);
        Assert.DoesNotContain("True-Client-IP", claves);
        Assert.Contains("X-Forwarded-For", claves);
        Assert.Contains("Authorization", claves);
    }
}
