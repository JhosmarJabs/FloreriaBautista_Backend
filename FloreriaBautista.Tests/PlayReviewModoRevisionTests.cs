using System.Reflection;
using System.Text;
using FloreriaBautista.Controllers;
using FloreriaBautista.Controllers.Admin;
using FloreriaBautista.PlayReview;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FloreriaBautista.Tests;

/// <summary>
/// Instancia REVIEW (APP_ENTORNO=play_review): firma del borde, bloqueo de operaciones
/// de infraestructura y validación del arranque.
/// </summary>
public class PlayReviewModoRevisionTests
{
    private const string Secreto = "secreto-de-prueba-de-al-menos-32-caracteres-xx";

    // ── Firma ────────────────────────────────────────────────────

    [Fact]
    public void Firma_Recien_Creada_Es_Valida()
    {
        var ahora = DateTimeOffset.UtcNow;
        var h = FirmaBorde.CrearHeader(Secreto, ahora, "post", "/api/auth/login");
        Assert.Equal(ResultadoFirma.Valida, FirmaBorde.Validar(Secreto, h, "POST", "/api/auth/login", ahora));
    }

    [Fact]
    public void Firma_Con_Otro_Secreto_Metodo_O_Ruta_Es_Invalida()
    {
        var ahora = DateTimeOffset.UtcNow;
        var h = FirmaBorde.CrearHeader(Secreto, ahora, "GET", "/api/admin/orders");
        Assert.Equal(ResultadoFirma.Invalida, FirmaBorde.Validar(Secreto + "x", h, "GET", "/api/admin/orders", ahora));
        Assert.Equal(ResultadoFirma.Invalida, FirmaBorde.Validar(Secreto, h, "DELETE", "/api/admin/orders", ahora));
        Assert.Equal(ResultadoFirma.Invalida, FirmaBorde.Validar(Secreto, h, "GET", "/api/admin/backups", ahora));
    }

    [Fact]
    public void Firma_Con_Timestamp_Manipulado_Es_Invalida()
    {
        var ahora = DateTimeOffset.UtcNow;
        var h = FirmaBorde.CrearHeader(Secreto, ahora.AddMinutes(-30), "GET", "/api/x");
        var firma = h.Split('.')[1];
        var manipulado = $"{ahora.ToUnixTimeSeconds()}.{firma}";
        Assert.Equal(ResultadoFirma.Invalida, FirmaBorde.Validar(Secreto, manipulado, "GET", "/api/x", ahora));
    }

    [Theory]
    [InlineData(-301)]
    [InlineData(301)]
    public void Firma_Fuera_De_La_Ventana_De_300s_Esta_Caducada(int segundos)
    {
        var ahora = DateTimeOffset.UtcNow;
        var h = FirmaBorde.CrearHeader(Secreto, ahora.AddSeconds(segundos), "GET", "/api/x");
        Assert.Equal(ResultadoFirma.Caducada, FirmaBorde.Validar(Secreto, h, "GET", "/api/x", ahora));
    }

    [Fact]
    public void Firma_En_El_Limite_De_300s_Sigue_Valida()
    {
        var ahora = DateTimeOffset.UtcNow;
        var h = FirmaBorde.CrearHeader(Secreto, ahora.AddSeconds(-300), "GET", "/api/x");
        Assert.Equal(ResultadoFirma.Valida, FirmaBorde.Validar(Secreto, h, "GET", "/api/x", ahora));
    }

    [Theory]
    [InlineData(null, ResultadoFirma.Ausente)]
    [InlineData("", ResultadoFirma.Ausente)]
    [InlineData("sin-punto", ResultadoFirma.Malformada)]
    [InlineData("abc.def", ResultadoFirma.Malformada)]
    [InlineData("123.x", ResultadoFirma.Malformada)]
    [InlineData("1.2.3", ResultadoFirma.Malformada)]
    public void Firma_Ausente_O_Mal_Formada(string? header, ResultadoFirma esperado)
    {
        Assert.Equal(esperado, FirmaBorde.Validar(Secreto, header, "GET", "/api/x", DateTimeOffset.UtcNow));
    }

    // ── Middleware de firma en REVIEW ────────────────────────────

    private static (PlayReviewFirmaBordeMiddleware Mw, Func<bool> Siguio) CrearFirma()
    {
        var siguio = false;
        var mw = new PlayReviewFirmaBordeMiddleware(_ => { siguio = true; return Task.CompletedTask; },
            new OpcionesModoRevision { Activo = true, Secreto = Secreto },
            NullLogger<PlayReviewFirmaBordeMiddleware>.Instance);
        return (mw, () => siguio);
    }

    private static DefaultHttpContext Peticion(string metodo, string ruta, string? firma = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = metodo;
        ctx.Request.Path   = ruta;
        if (firma is not null) ctx.Request.Headers[PlayReviewEntorno.HeaderFirma] = firma;
        ctx.Response.Body = new MemoryStream();
        return ctx;
    }

    private static string Cuerpo(HttpContext ctx)
    {
        ctx.Response.Body.Position = 0;
        return new StreamReader(ctx.Response.Body, Encoding.UTF8).ReadToEnd();
    }

    [Fact]
    public async Task Review_Rechaza_Peticion_Sin_Firma_Con_403()
    {
        var (mw, siguio) = CrearFirma();
        var ctx = Peticion("POST", "/api/auth/login");
        await mw.InvokeAsync(ctx);

        Assert.False(siguio());
        Assert.Equal(403, ctx.Response.StatusCode);
        Assert.Contains("Direct access is not allowed", Cuerpo(ctx));
    }

    [Fact]
    public async Task Review_Rechaza_Firma_Caducada_E_Invalida()
    {
        var (mw, siguio) = CrearFirma();
        var vieja = Peticion("GET", "/api/admin/orders",
            FirmaBorde.CrearHeader(Secreto, DateTimeOffset.UtcNow.AddMinutes(-10), "GET", "/api/admin/orders"));
        await mw.InvokeAsync(vieja);
        Assert.Equal(403, vieja.Response.StatusCode);

        var otra = Peticion("GET", "/api/admin/orders",
            FirmaBorde.CrearHeader("otro-secreto-de-al-menos-32-caracteres-yyyy", DateTimeOffset.UtcNow, "GET", "/api/admin/orders"));
        await mw.InvokeAsync(otra);
        Assert.Equal(403, otra.Response.StatusCode);
        Assert.False(siguio());
    }

    [Fact]
    public async Task Review_Acepta_Firma_Valida_Y_Quita_El_Header()
    {
        var (mw, siguio) = CrearFirma();
        var ctx = Peticion("GET", "/api/admin/orders",
            FirmaBorde.CrearHeader(Secreto, DateTimeOffset.UtcNow, "GET", "/api/admin/orders"));
        await mw.InvokeAsync(ctx);

        Assert.True(siguio());
        Assert.False(ctx.Request.Headers.ContainsKey(PlayReviewEntorno.HeaderFirma));
    }

    [Fact]
    public async Task Review_Deja_Pasar_Solo_El_Get_De_Ping_Sin_Firma()
    {
        var (mw, siguio) = CrearFirma();
        await mw.InvokeAsync(Peticion("GET", "/api/ping"));
        Assert.True(siguio());

        var (mw2, siguio2) = CrearFirma();
        var post = Peticion("POST", "/api/ping");
        await mw2.InvokeAsync(post);
        Assert.False(siguio2());
        Assert.Equal(403, post.Response.StatusCode);
    }

    // ── Bloqueo de operaciones ───────────────────────────────────

    private static Endpoint EndpointCon(params object[] metadata) =>
        new(_ => Task.CompletedTask, new EndpointMetadataCollection(metadata), "prueba");

    [Fact]
    public async Task Endpoint_Bloqueado_Da_403_En_Revision()
    {
        var siguio = false;
        var mw = new PlayReviewBloqueoMiddleware(_ => { siguio = true; return Task.CompletedTask; },
            NullLogger<PlayReviewBloqueoMiddleware>.Instance);
        var ctx = Peticion("POST", "/api/admin/backups/full");
        ctx.SetEndpoint(EndpointCon(new BloqueadoEnRevisionAttribute()));

        await mw.InvokeAsync(ctx);

        Assert.False(siguio);
        Assert.Equal(403, ctx.Response.StatusCode);
        var cuerpo = Cuerpo(ctx);
        Assert.Contains("deshabilitada en el entorno de revisión", cuerpo);
        Assert.Contains("disabled in the review environment", cuerpo);
    }

    [Fact]
    public async Task Endpoint_Sin_Marca_Pasa()
    {
        var siguio = false;
        var mw = new PlayReviewBloqueoMiddleware(_ => { siguio = true; return Task.CompletedTask; },
            NullLogger<PlayReviewBloqueoMiddleware>.Instance);
        var ctx = Peticion("GET", "/api/admin/orders");
        ctx.SetEndpoint(EndpointCon());

        await mw.InvokeAsync(ctx);
        Assert.True(siguio);
    }

    [Theory]
    [InlineData(typeof(AdminBackupsController))]
    [InlineData(typeof(AdminDatabaseController))]
    [InlineData(typeof(AdminSchedulerController))]
    [InlineData(typeof(AdminWhatsAppController))]
    [InlineData(typeof(AdminImportExportController))]
    [InlineData(typeof(DevController))]
    [InlineData(typeof(AlexaController))]
    [InlineData(typeof(WhatsAppInternalController))]
    [InlineData(typeof(PaymentsController))]
    public void Controladores_De_Infraestructura_Estan_Bloqueados(Type controlador)
    {
        Assert.NotNull(controlador.GetCustomAttribute<BloqueadoEnRevisionAttribute>());
    }

    [Theory]
    [InlineData("Crear", true)]
    [InlineData("Actualizar", true)]
    [InlineData("AsignarResponsableTurno", true)]
    [InlineData("QuitarResponsableTurno", true)]
    [InlineData("Listar", false)]
    [InlineData("Detalle", false)]
    [InlineData("ObtenerResponsableTurno", false)]
    public void Gestion_De_Usuarios_Bloquea_Solo_Las_Mutaciones(string accion, bool bloqueada)
    {
        var metodo = typeof(AdminUsersController).GetMethod(accion)!;
        Assert.Equal(bloqueada, metodo.GetCustomAttribute<BloqueadoEnRevisionAttribute>() is not null);
        Assert.Null(typeof(AdminUsersController).GetCustomAttribute<BloqueadoEnRevisionAttribute>());
    }

    [Fact]
    public void Los_Modulos_De_Negocio_No_Estan_Bloqueados()
    {
        // Google tiene que poder ver toda la app: pedidos, ventas, inventario, catálogo, caja.
        var negocio = typeof(PingController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .Where(t => t.Name is "OrdersController" or "AdminOrdersController" or "AdminInventoryController"
                        or "AdminProductsController" or "EmployeeOperationsController" or "AdminReportsController"
                        or "CustomersController" or "InstantSalesController" or "AdminInstantSalesController"
                        or "QuickSaleTemplatesController" or "AuthController" or "UsersController" or "PingController"
                        // Lo que usa la venta de mostrador de los testers de Google Play: catálogo,
                        // categorías, CMS (dirección de tienda), notificaciones y tienda.
                        or "EmployeeProductsController" or "CategoriesController" or "ProductsController"
                        or "CmsController" or "NotificationsController" or "StoreController")
            .ToList();

        Assert.True(negocio.Count >= 18);
        foreach (var t in negocio)
        {
            Assert.Null(t.GetCustomAttribute<BloqueadoEnRevisionAttribute>());
            Assert.DoesNotContain(t.GetMethods(), m => m.GetCustomAttribute<BloqueadoEnRevisionAttribute>() is not null);
        }
    }

    // ── Arranque de la instancia de revisión ─────────────────────

    private static Func<string, string?> Entorno(Dictionary<string, string?> vars) =>
        k => vars.TryGetValue(k, out var v) ? v : null;

    [Fact]
    public void Sin_App_Entorno_El_Modo_Revision_Esta_Apagado()
    {
        Assert.False(OpcionesModoRevision.Desde(_ => null, esDevelopment: false).Activo);
        Assert.False(OpcionesModoRevision.Desde(Entorno(new() { ["APP_ENTORNO"] = "production" }), false).Activo);
    }

    [Fact]
    public void Modo_Revision_Bien_Configurado_Arranca()
    {
        var o = OpcionesModoRevision.Desde(Entorno(new()
        {
            ["APP_ENTORNO"] = "Play_Review", ["REVIEW_EDGE_SECRET"] = Secreto,
        }), esDevelopment: false);
        Assert.True(o.Activo);
    }

    [Fact]
    public void Modo_Revision_No_Arranca_En_Development_Ni_Sin_Secreto()
    {
        Assert.Throws<InvalidOperationException>(() => OpcionesModoRevision.Desde(Entorno(new()
        {
            ["APP_ENTORNO"] = "play_review", ["REVIEW_EDGE_SECRET"] = Secreto,
        }), esDevelopment: true));

        Assert.Throws<InvalidOperationException>(() => OpcionesModoRevision.Desde(Entorno(new()
        {
            ["APP_ENTORNO"] = "play_review",
        }), esDevelopment: false));
    }

    [Theory]
    [InlineData("MERCADOPAGO_ACCESS_TOKEN")]
    [InlineData("EVOLUTION_API_KEY")]
    [InlineData("ALEXA_API_KEY")]
    [InlineData("CLOUDINARY_API_SECRET")]
    [InlineData("GOOGLE_CLIENT_SECRET")]
    [InlineData("DB_ADMIN_PASSWORD")]
    [InlineData("REVIEW_UPSTREAM_URL")]
    public void Modo_Revision_No_Arranca_Con_Variables_De_Produccion(string variable)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => OpcionesModoRevision.Desde(Entorno(new()
        {
            ["APP_ENTORNO"] = "play_review", ["REVIEW_EDGE_SECRET"] = Secreto, [variable] = "valor",
        }), esDevelopment: false));
        Assert.Contains(variable, ex.Message);
    }
}
