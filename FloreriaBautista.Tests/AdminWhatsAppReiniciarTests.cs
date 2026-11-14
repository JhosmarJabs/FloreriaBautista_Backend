using System.Security.Claims;
using System.Text.Json;
using FloreriaBautista.Controllers.Admin;
using FloreriaBautista.Data;
using FloreriaBautista.Hubs;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Services.Audit;
using FloreriaBautista.Services.Notifications;
using FloreriaBautista.Services.WhatsApp;
using FloreriaBautista.Tests.TestHelpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FloreriaBautista.Tests;

[CollectionDefinition("WhatsAppEnv", DisableParallelization = true)]
public class WhatsAppEnvCollection { }

/// <summary>
/// BATCH-02 notificaciones-whatsapp: AdminWhatsAppController.Reiniciar («Reconectar») con Evolution
/// simulado, EF InMemory y variables de entorno de prueba (nunca se llama a la URL).
/// </summary>
[Collection("WhatsAppEnv")]
public class AdminWhatsAppReiniciarTests : IDisposable
{
    private const string UrlPrueba = "http://127.0.0.1:9";
    private const string ClavePrueba = "clave-de-prueba-no-real";

    private readonly string? _urlPrevia = Environment.GetEnvironmentVariable("EVOLUTION_API_URL");
    private readonly string? _clavePrevia = Environment.GetEnvironmentVariable("EVOLUTION_API_KEY");

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("EVOLUTION_API_URL", _urlPrevia);
        Environment.SetEnvironmentVariable("EVOLUTION_API_KEY", _clavePrevia);
    }

    private sealed class Esc
    {
        public AppDbContext Db = null!;
        public EvolucionFalsa Evo = null!;
        public WhatsAppEstadoActual Estado = null!;
        public WhatsAppConfig Config = null!;
        public Guid AdminId;

        public AdminWhatsAppController Nuevo()
        {
            var monitor = new WhatsAppConnectionMonitor(Db,
                new NotificationService(Db, NullLogger<NotificationService>.Instance),
                new HubFalso(), Estado, NullLogger<WhatsAppConnectionMonitor>.Instance);
            var versiones = new WhatsAppVersionProvider(new FabricaHttpFalsa(),
                new MemoryCache(new MemoryCacheOptions()), NullLogger<WhatsAppVersionProvider>.Instance);
            return new AdminWhatsAppController(Db, Evo, new NullWhatsAppNotifier(), monitor, versiones,
                Config, Estado, new AuditService(Db, NullLogger<AuditService>.Instance))
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                        {
                            new Claim("sub", AdminId.ToString()), new Claim(ClaimTypes.Role, "ADMIN")
                        }, "prueba"))
                    }
                }
            };
        }

        public async Task<(int Codigo, bool Success, string Message, string? Estado)> Llamar()
        {
            var r = await Nuevo().Reiniciar(CancellationToken.None);
            var o = (ObjectResult)r;
            var codigo = o.StatusCode ?? (o is OkObjectResult ? 200 : o is ConflictObjectResult ? 409 : 0);
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(o.Value));
            var raiz = doc.RootElement;
            string? estado = null;
            if (raiz.TryGetProperty("Data", out var d) && d.ValueKind == JsonValueKind.Object
                && d.TryGetProperty("estado", out var e))
                estado = e.GetString();
            return (codigo, raiz.GetProperty("Success").GetBoolean(), raiz.GetProperty("Message").GetString()!, estado);
        }

        public WhatsAppSettings Settings => Db.WhatsAppSettings.AsNoTracking().Single();
        public List<AuditLog> Auditorias => Db.AuditLogs.AsNoTracking()
            .Where(a => a.Accion == "WHATSAPP_REINICIO").ToList();
    }

    private Esc Crear(bool configurado, string estadoConexion, EvolutionResultado<bool> reinicio,
        EvolutionResultado<string>? estado = null, WhatsAppEstadoActual? estadoActual = null,
        DateTime? ultimaVerificacion = null)
    {
        Environment.SetEnvironmentVariable("EVOLUTION_API_URL", configurado ? UrlPrueba : null);
        Environment.SetEnvironmentVariable("EVOLUTION_API_KEY", configurado ? ClavePrueba : null);

        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"wa-reiniciar-{Guid.NewGuid()}").Options);
        db.WhatsAppSettings.Add(new WhatsAppSettings
        {
            Id = 1, EstadoConexion = estadoConexion, Habilitado = true,
            UltimaVerificacionEn = ultimaVerificacion
        });
        db.SaveChanges();

        return new Esc
        {
            Db = db,
            Evo = new EvolucionFalsa { Reinicio = reinicio, Estado = estado ?? EvolutionResultado<string>.Ok("open") },
            Estado = estadoActual ?? new WhatsAppEstadoActual { ArrancoEn = DateTime.UtcNow.AddMinutes(-10) },
            Config = new WhatsAppConfig(),
            AdminId = Guid.NewGuid()
        };
    }

    [Fact]
    public async Task BE_WA_REI_001_SinConfigurar_503_NoLlamaAEvolution()
    {
        var c = Crear(false, "NO_CONFIGURADO", EvolutionResultado<bool>.Ok(true));
        Assert.False(c.Config.Configurado);

        var r = await c.Llamar();

        Assert.Equal(503, r.Codigo);
        Assert.False(r.Success);
        Assert.Equal("WhatsApp no está configurado en el servidor.", r.Message);
        Assert.Equal(0, c.Evo.LlamadasReinicio);
        Assert.Equal(0, c.Evo.LlamadasEstado);
        Assert.Empty(c.Auditorias);
        Assert.Null(c.Estado.UltimoReinicioEn);
    }

    [Fact]
    public async Task BE_WA_REI_002_EvolutionAcepta_200_ConEstado_LoRegistraYAudita()
    {
        var c = Crear(true, "DESCONECTADO", EvolutionResultado<bool>.Ok(true),
            EvolutionResultado<string>.Ok("connecting"));
        Assert.True(c.Config.Configurado);

        var r = await c.Llamar();

        Assert.Equal(200, r.Codigo);
        Assert.True(r.Success);
        Assert.Equal("Reconexión solicitada.", r.Message);
        Assert.Equal("CONECTANDO", r.Estado);
        Assert.Equal(1, c.Evo.LlamadasReinicio);
        Assert.Equal(1, c.Evo.LlamadasEstado);
        var s = c.Settings;
        Assert.Equal("CONECTANDO", s.EstadoConexion);
        Assert.NotNull(s.UltimaVerificacionEn);
        Assert.True(DateTime.UtcNow - s.UltimaVerificacionEn!.Value < TimeSpan.FromSeconds(10));

        var aud = Assert.Single(c.Auditorias);
        Assert.Equal("WhatsAppSettings", aud.Entidad);
        Assert.Equal("1", aud.EntidadId);
        Assert.Equal(c.AdminId, aud.UsuarioId);
        Assert.DoesNotContain(UrlPrueba, aud.Detalles ?? "");
        Assert.DoesNotContain(ClavePrueba, aud.Detalles ?? "");

        Assert.NotNull(c.Estado.UltimoReinicioEn);
        Assert.True(DateTime.UtcNow - c.Estado.UltimoReinicioEn!.Value < TimeSpan.FromSeconds(10));

        // Segundo escenario: Evolution informa "open" -> CONECTADO
        var c2 = Crear(true, "DESCONECTADO", EvolutionResultado<bool>.Ok(true),
            EvolutionResultado<string>.Ok("open"));
        var r2 = await c2.Llamar();
        Assert.Equal(200, r2.Codigo);
        Assert.Equal("CONECTADO", r2.Estado);
    }

    [Fact]
    public async Task BE_WA_REI_003_SegundoReinicioAntesDe60s_409_NoLlamaAEvolution()
    {
        var c = Crear(true, "DESCONECTADO", EvolutionResultado<bool>.Ok(true),
            EvolutionResultado<string>.Ok("connecting"));

        var r1 = await c.Llamar();
        Assert.Equal(200, r1.Codigo);

        var r2 = await c.Llamar();
        Assert.Equal(409, r2.Codigo);
        Assert.False(r2.Success);
        Assert.Equal("Espera un minuto antes de volver a reconectar.", r2.Message);
        Assert.Equal(1, c.Evo.LlamadasReinicio);
        Assert.Equal(1, c.Evo.LlamadasEstado);
        Assert.Single(c.Auditorias);
    }

    [Fact]
    public async Task BE_WA_REI_004_EvolutionInalcanzable_503_SinEstadoIntermedio_PermiteReintentar()
    {
        var sembrada = DateTime.UtcNow.AddHours(-2);
        var c = Crear(true, "CONECTADO",
            EvolutionResultado<bool>.Error(EvolutionCodigos.Inalcanzable, "sin respuesta"),
            ultimaVerificacion: sembrada);
        var antes = c.Settings;

        var r = await c.Llamar();

        Assert.Equal(503, r.Codigo);
        Assert.False(r.Success);
        Assert.Equal("El servicio de WhatsApp no responde; si acaba de despertar, intenta en un minuto.", r.Message);
        Assert.Equal(0, c.Evo.LlamadasEstado);
        Assert.Null(c.Estado.UltimoReinicioEn);
        var s = c.Settings;
        Assert.Equal("CONECTADO", s.EstadoConexion);
        Assert.Equal(antes.ActualizadoEn, s.ActualizadoEn);
        Assert.Equal(antes.UltimaVerificacionEn, s.UltimaVerificacionEn);
        Assert.Empty(c.Auditorias);

        var r2 = await c.Llamar();
        Assert.Equal(503, r2.Codigo);
        Assert.Equal(2, c.Evo.LlamadasReinicio);
    }

    [Theory]
    [InlineData(EvolutionCodigos.Http, "HTTP 500")]
    [InlineData(EvolutionCodigos.NoEncontrado, "sin instancia")]
    public async Task BE_WA_REI_005_EvolutionRechaza_502_NoMarcaLimite(string codigo, string mensaje)
    {
        var c = Crear(true, "CONECTADO", EvolutionResultado<bool>.Error(codigo, mensaje));

        var r = await c.Llamar();

        Assert.Equal(502, r.Codigo);
        Assert.False(r.Success);
        Assert.Equal("WhatsApp rechazó la reconexión; inténtalo de nuevo más tarde.", r.Message);
        Assert.Null(c.Estado.UltimoReinicioEn);
        Assert.Equal(0, c.Evo.LlamadasEstado);
        Assert.Empty(c.Auditorias);
        Assert.Equal("CONECTADO", c.Settings.EstadoConexion);
    }

    [Fact]
    public async Task BE_WA_REI_006_AceptaPeroEstadoNoConsultable_200_ConEstadoGuardado()
    {
        var sembrada = DateTime.UtcNow.AddHours(-1);
        var c = Crear(true, "DESCONECTADO", EvolutionResultado<bool>.Ok(true),
            EvolutionResultado<string>.Error(EvolutionCodigos.Inalcanzable, "sin respuesta"),
            ultimaVerificacion: sembrada);
        var antes = c.Settings.UltimaVerificacionEn;

        var r = await c.Llamar();

        Assert.Equal(200, r.Codigo);
        Assert.True(r.Success);
        Assert.Equal("Reconexión solicitada.", r.Message);
        Assert.Equal("DESCONECTADO", r.Estado);
        Assert.Equal(antes, c.Settings.UltimaVerificacionEn);
        Assert.Equal("DESCONECTADO", c.Settings.EstadoConexion);
        Assert.Single(c.Auditorias);
        Assert.NotNull(c.Estado.UltimoReinicioEn);
    }

    [Fact]
    public async Task BE_WA_REI_007_BordeDe60s_59sDa409_61sVuelveALlamar()
    {
        var estado59 = new WhatsAppEstadoActual { ArrancoEn = DateTime.UtcNow.AddMinutes(-10) };
        estado59.UltimoReinicioEn = DateTime.UtcNow.AddSeconds(-59);
        var c1 = Crear(true, "DESCONECTADO", EvolutionResultado<bool>.Ok(true),
            EvolutionResultado<string>.Ok("open"), estado59);
        var r1 = await c1.Llamar();
        Assert.Equal(409, r1.Codigo);
        Assert.Equal(0, c1.Evo.LlamadasReinicio);

        var estado61 = new WhatsAppEstadoActual { ArrancoEn = DateTime.UtcNow.AddMinutes(-10) };
        estado61.UltimoReinicioEn = DateTime.UtcNow.AddSeconds(-61);
        var c2 = Crear(true, "DESCONECTADO", EvolutionResultado<bool>.Ok(true),
            EvolutionResultado<string>.Ok("open"), estado61);
        var r2 = await c2.Llamar();
        Assert.Equal(200, r2.Codigo);
        Assert.Equal(1, c2.Evo.LlamadasReinicio);
    }

    // ── Auxiliares ───────────────────────────────────────────────

    private sealed class EvolucionFalsa : IEvolutionClient
    {
        public EvolutionResultado<bool> Reinicio = EvolutionResultado<bool>.Ok(true);
        public EvolutionResultado<string> Estado = EvolutionResultado<string>.Ok("open");
        public int LlamadasReinicio;
        public int LlamadasEstado;

        public Task<EvolutionResultado<bool>> ReiniciarInstanciaAsync(CancellationToken ct = default)
        { LlamadasReinicio++; return Task.FromResult(Reinicio); }
        public Task<EvolutionResultado<string>> ObtenerEstadoAsync(CancellationToken ct = default)
        { LlamadasEstado++; return Task.FromResult(Estado); }

        public Task<EvolutionResultado<bool>> CrearInstanciaSiNoExisteAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EvolutionResultado<EvolutionInstancia>> ObtenerInstanciaAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EvolutionResultado<EvolutionQr>> ConectarAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EvolutionResultado<bool>> DesconectarAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EvolutionResultado<bool>> ConfigurarWebhookAsync(string url, string token, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EvolutionResultado<bool>> VerificarNumeroAsync(string numero, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EvolutionResultado<bool>> EnviarTextoAsync(string numero, string texto, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FabricaHttpFalsa : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => throw new NotSupportedException();
    }

    private sealed class HubFalso : IHubContext<VentaInstantaneaHub>
    {
        public IHubClients Clients => new Clientes();
        public IGroupManager Groups => throw new NotSupportedException();
        private sealed class Clientes : IHubClients
        {
            public IClientProxy All => new Proxy();
            public IClientProxy AllExcept(IReadOnlyList<string> e) => new Proxy();
            public IClientProxy Client(string id) => new Proxy();
            public IClientProxy Clients(IReadOnlyList<string> ids) => new Proxy();
            public IClientProxy Group(string g) => new Proxy();
            public IClientProxy GroupExcept(string g, IReadOnlyList<string> e) => new Proxy();
            public IClientProxy Groups(IReadOnlyList<string> gs) => new Proxy();
            public IClientProxy User(string u) => new Proxy();
            public IClientProxy Users(IReadOnlyList<string> us) => new Proxy();
        }
        private sealed class Proxy : IClientProxy
        {
            public Task SendCoreAsync(string m, object?[] a, CancellationToken ct = default) => Task.CompletedTask;
        }
    }
}
