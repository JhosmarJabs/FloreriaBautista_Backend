using FloreriaBautista.Data;
using FloreriaBautista.Hubs;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Services.Notifications;
using FloreriaBautista.Services.WhatsApp;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FloreriaBautista.Tests;

/// <summary>
/// BATCH-01 notificaciones-whatsapp: gracia de arranque (U-B3), desviacion J y U-B3a de
/// WhatsAppConnectionMonitor.RegistrarEstadoAsync. Todo en memoria (EF InMemory + hub falso).
/// </summary>
public class WhatsAppGraciaArranqueTests
{
    private const string NumeroFalso = "520000000000";
    private static readonly Guid AdminId = Guid.NewGuid();
    private static readonly Guid EmpleadoId = Guid.NewGuid();

    private sealed class Ctx
    {
        public AppDbContext Db = null!;
        public HubFalso Hub = null!;
        public WhatsAppEstadoActual Estado = null!;
        public WhatsAppConnectionMonitor Monitor = null!;

        public WhatsAppSettings Settings => Db.WhatsAppSettings.AsNoTracking().Single();
        public int Emisiones => Hub.Envios.Count(e => e.Destino == "admin" && e.Metodo == "WhatsAppEstado");
        public List<Notification> Notifs => Db.Notifications.AsNoTracking()
            .Where(n => n.Tipo == "WHATSAPP_DESCONECTADO").ToList();

        public Task Llamar(string estado, string origen = "monitor", string? numero = null)
            => Monitor.RegistrarEstadoAsync(estado, origen, numero);

        public void SimularTresMinutos()
        {
            var s = Db.WhatsAppSettings.Single();
            s.DesconectadoDesde = s.DesconectadoDesde!.Value.AddMinutes(-4);
            Db.SaveChanges();
        }
    }

    private static Ctx Crear(string estadoGuardado, DateTime arrancoEn, bool habilitado = true,
        DateTime? desconectadoDesde = null, bool avisoEnviado = false, string? numero = NumeroFalso)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"wa-gracia-{Guid.NewGuid()}").Options);

        var rolAdmin = new Role { Id = Guid.NewGuid(), Nombre = "ADMIN" };
        var rolEmp = new Role { Id = Guid.NewGuid(), Nombre = "EMPLEADO" };
        db.Roles.AddRange(rolAdmin, rolEmp);
        db.Users.Add(new User { Id = AdminId, Nombre = "QA", Apellido = "Admin", Correo = "qa-wa-admin@example.test", Estado = "ACTIVO" });
        db.Users.Add(new User { Id = EmpleadoId, Nombre = "QA", Apellido = "Empleado", Correo = "qa-wa-emp@example.test", Estado = "ACTIVO" });
        db.UserRoles.Add(new UserRole { UserId = AdminId, RoleId = rolAdmin.Id });
        db.UserRoles.Add(new UserRole { UserId = EmpleadoId, RoleId = rolEmp.Id });
        db.WhatsAppSettings.Add(new WhatsAppSettings
        {
            Id = 1,
            EstadoConexion = estadoGuardado,
            Habilitado = habilitado,
            NumeroVinculado = numero,
            DesconectadoDesde = desconectadoDesde,
            AvisoDesconexionEnviado = avisoEnviado,
        });
        db.SaveChanges();

        var hub = new HubFalso();
        var notifs = new NotificationService(db, NullLogger<NotificationService>.Instance);
        var estado = new WhatsAppEstadoActual { ArrancoEn = arrancoEn };
        var monitor = new WhatsAppConnectionMonitor(db, notifs, hub, estado,
            NullLogger<WhatsAppConnectionMonitor>.Instance);
        return new Ctx { Db = db, Hub = hub, Estado = estado, Monitor = monitor };
    }

    private static DateTime CercaDelLimite() =>
        DateTime.UtcNow - WhatsAppConfig.GraciaArranque + TimeSpan.FromSeconds(3);

    private static void AfirmarUnSoloAdmin(Ctx c)
    {
        var n = c.Notifs;
        Assert.Single(n);
        Assert.Equal(AdminId, n[0].DestinatarioUsuarioId);
        Assert.DoesNotContain(n, x => x.DestinatarioUsuarioId == EmpleadoId);
    }

    [Fact]
    public async Task BE_WA_GRA_001_DentroDeGracia_NoDisponibleYConectando_SeGuardaConectandoSinCaida()
    {
        var c = Crear("CONECTADO", DateTime.UtcNow);

        await c.Llamar(WhatsAppConstantes.ServicioNoDisponible, "monitor");
        var s = c.Settings;
        Assert.Equal("CONECTANDO", s.EstadoConexion);
        Assert.Null(s.DesconectadoDesde);
        Assert.True(c.Estado.VeniaConectadoAntesDeGracia);
        Assert.Equal(NumeroFalso, s.NumeroVinculado);
        Assert.Equal(0, c.Emisiones);

        await c.Llamar(WhatsAppConstantes.Conectando, "webhook");
        s = c.Settings;
        Assert.Equal("CONECTANDO", s.EstadoConexion);
        Assert.Null(s.DesconectadoDesde);
        Assert.True(c.Estado.VeniaConectadoAntesDeGracia);
        Assert.Equal(0, c.Emisiones);
        Assert.Empty(c.Notifs);
    }

    [Fact]
    public async Task BE_WA_GRA_002_GraciaVencidaSinRespuesta_CuentaCaidaYAvisaUnaVezAlAdmin()
    {
        var c = Crear("CONECTADO", CercaDelLimite());

        await c.Llamar(WhatsAppConstantes.ServicioNoDisponible);
        Assert.Equal("CONECTANDO", c.Settings.EstadoConexion);
        Assert.True(c.Estado.VeniaConectadoAntesDeGracia);

        await Task.Delay(TimeSpan.FromSeconds(4));
        await c.Llamar(WhatsAppConstantes.ServicioNoDisponible);

        var s = c.Settings;
        Assert.Equal("SERVICIO_NO_DISPONIBLE", s.EstadoConexion);
        Assert.NotNull(s.DesconectadoDesde);
        Assert.True((DateTime.UtcNow - s.DesconectadoDesde!.Value) < TimeSpan.FromSeconds(10));
        Assert.False(c.Estado.VeniaConectadoAntesDeGracia);
        Assert.Equal(1, c.Emisiones);
        Assert.Empty(c.Notifs);
        Assert.False(s.AvisoDesconexionEnviado);

        c.SimularTresMinutos();
        await c.Llamar(WhatsAppConstantes.ServicioNoDisponible);
        AfirmarUnSoloAdmin(c);
        Assert.True(c.Settings.AvisoDesconexionEnviado);

        await c.Llamar(WhatsAppConstantes.ServicioNoDisponible);
        Assert.Single(c.Notifs);
    }

    [Fact]
    public async Task BE_WA_GRA_003_CloseDentroDeGracia_TrasEnmascarado_CuentaCaidaEnEseMomento()
    {
        var c = Crear("CONECTADO", DateTime.UtcNow);

        await c.Llamar(WhatsAppConstantes.ServicioNoDisponible);
        Assert.Equal("CONECTANDO", c.Settings.EstadoConexion);
        Assert.True(c.Estado.VeniaConectadoAntesDeGracia);

        await c.Llamar(WhatsAppConstantes.Desconectado, "webhook");
        var s = c.Settings;
        Assert.Equal("DESCONECTADO", s.EstadoConexion);
        Assert.NotNull(s.DesconectadoDesde);
        Assert.Null(s.NumeroVinculado);
        Assert.False(c.Estado.VeniaConectadoAntesDeGracia);
        Assert.Equal(1, c.Emisiones);
        Assert.Empty(c.Notifs);

        c.SimularTresMinutos();
        await c.Llamar(WhatsAppConstantes.Desconectado);
        AfirmarUnSoloAdmin(c);
        Assert.True(c.Settings.AvisoDesconexionEnviado);

        await c.Llamar(WhatsAppConstantes.Desconectado);
        Assert.Single(c.Notifs);
    }

    [Fact]
    public async Task BE_WA_GRA_004_CloseTrasGraciaVencida_TrasEnmascarado_CuentaCaida()
    {
        var c = Crear("CONECTADO", CercaDelLimite());

        await c.Llamar(WhatsAppConstantes.ServicioNoDisponible);
        Assert.Equal("CONECTANDO", c.Settings.EstadoConexion);
        Assert.True(c.Estado.VeniaConectadoAntesDeGracia);

        await Task.Delay(TimeSpan.FromSeconds(4));
        await c.Llamar(WhatsAppConstantes.Desconectado, "webhook");
        var s = c.Settings;
        Assert.Equal("DESCONECTADO", s.EstadoConexion);
        Assert.NotNull(s.DesconectadoDesde);
        Assert.Null(s.NumeroVinculado);
        Assert.False(c.Estado.VeniaConectadoAntesDeGracia);
        Assert.Equal(1, c.Emisiones);
        Assert.Empty(c.Notifs);

        c.SimularTresMinutos();
        await c.Llamar(WhatsAppConstantes.Desconectado);
        AfirmarUnSoloAdmin(c);
        Assert.True(c.Settings.AvisoDesconexionEnviado);
    }

    [Fact]
    public async Task BE_WA_GRA_005_ConectandoPersisteTrasGracia_TrasEnmascarado_CuentaCaida()
    {
        var c = Crear("CONECTADO", CercaDelLimite());

        await c.Llamar(WhatsAppConstantes.ServicioNoDisponible);
        Assert.Equal("CONECTANDO", c.Settings.EstadoConexion);
        Assert.True(c.Estado.VeniaConectadoAntesDeGracia);

        await Task.Delay(TimeSpan.FromSeconds(4));
        await c.Llamar(WhatsAppConstantes.Conectando, "webhook");
        var s = c.Settings;
        Assert.Equal("CONECTANDO", s.EstadoConexion);
        Assert.NotNull(s.DesconectadoDesde);
        Assert.Equal(NumeroFalso, s.NumeroVinculado);
        Assert.False(c.Estado.VeniaConectadoAntesDeGracia);
        Assert.Equal(1, c.Emisiones);
        Assert.Empty(c.Notifs);

        c.SimularTresMinutos();
        await c.Llamar(WhatsAppConstantes.Conectando);
        AfirmarUnSoloAdmin(c);
        Assert.True(c.Settings.AvisoDesconexionEnviado);
    }

    [Fact]
    public async Task BE_WA_GRA_006_OpenDentroDeGracia_QuedaNormalYCaidaPosteriorSeCuenta()
    {
        var c = Crear("CONECTADO", DateTime.UtcNow);

        await c.Llamar(WhatsAppConstantes.ServicioNoDisponible);
        Assert.Equal("CONECTANDO", c.Settings.EstadoConexion);
        Assert.True(c.Estado.VeniaConectadoAntesDeGracia);

        await c.Llamar(WhatsAppConstantes.Conectado, "monitor", NumeroFalso);
        var s = c.Settings;
        Assert.Equal("CONECTADO", s.EstadoConexion);
        Assert.Null(s.DesconectadoDesde);
        Assert.False(s.AvisoDesconexionEnviado);
        Assert.False(c.Estado.VeniaConectadoAntesDeGracia);
        Assert.Empty(c.Notifs);
        foreach (var e in c.Hub.Envios.Where(e => e.Metodo == "WhatsAppEstado"))
        {
            var estadoEmitido = e.Args[0]!.GetType().GetProperty("estado")!.GetValue(e.Args[0]) as string;
            Assert.Equal("CONECTADO", estadoEmitido);
        }

        await c.Llamar(WhatsAppConstantes.Desconectado, "webhook");
        s = c.Settings;
        Assert.Equal("DESCONECTADO", s.EstadoConexion);
        Assert.NotNull(s.DesconectadoDesde);
        Assert.Null(s.NumeroVinculado);
        Assert.False(c.Estado.VeniaConectadoAntesDeGracia);

        c.SimularTresMinutos();
        await c.Llamar(WhatsAppConstantes.Desconectado);
        AfirmarUnSoloAdmin(c);
    }

    [Theory]
    [InlineData("DESCONECTADO")]
    [InlineData("SERVICIO_NO_DISPONIBLE")]
    [InlineData("NO_CONFIGURADO")]
    public async Task BE_WA_GRA_007_ArranqueYaDesconectado_NoAvisa(string guardado)
    {
        var c = Crear(guardado, CercaDelLimite());

        await c.Llamar(WhatsAppConstantes.ServicioNoDisponible);
        Assert.Equal("CONECTANDO", c.Settings.EstadoConexion);
        Assert.False(c.Estado.VeniaConectadoAntesDeGracia);

        await Task.Delay(TimeSpan.FromSeconds(4));
        await c.Llamar(WhatsAppConstantes.ServicioNoDisponible);
        await c.Llamar(WhatsAppConstantes.ServicioNoDisponible);
        await c.Llamar(WhatsAppConstantes.ServicioNoDisponible);

        var s = c.Settings;
        Assert.Equal("SERVICIO_NO_DISPONIBLE", s.EstadoConexion);
        Assert.Null(s.DesconectadoDesde);
        Assert.False(s.AvisoDesconexionEnviado);
        Assert.False(c.Estado.VeniaConectadoAntesDeGracia);
        Assert.Empty(c.Notifs);
        Assert.Equal(0, c.Emisiones);
    }

    [Fact]
    public async Task BE_WA_GRA_008_CaidaPrevia_YaAvisada_NoRepiteNiMueveLaHora()
    {
        var sembrada = DateTime.UtcNow.AddHours(-1);
        var c = Crear("DESCONECTADO", CercaDelLimite(), desconectadoDesde: sembrada,
            avisoEnviado: true, numero: null);

        await c.Llamar(WhatsAppConstantes.ServicioNoDisponible);
        await Task.Delay(TimeSpan.FromSeconds(4));
        await c.Llamar(WhatsAppConstantes.ServicioNoDisponible);
        await c.Llamar(WhatsAppConstantes.ServicioNoDisponible);

        var s = c.Settings;
        Assert.Equal(sembrada, s.DesconectadoDesde);
        Assert.True(s.AvisoDesconexionEnviado);
        Assert.Empty(c.Notifs);
        Assert.False(c.Estado.VeniaConectadoAntesDeGracia);
    }

    [Theory]
    [InlineData("SERVICIO_NO_DISPONIBLE")]
    [InlineData("CONECTANDO")]
    [InlineData("DESCONECTADO")]
    public async Task BE_WA_GRA_009_SinGracia_CaidaDesdeConectadoSeCuentaDeInmediato(string entrante)
    {
        var c = Crear("CONECTADO", DateTime.UtcNow.AddMinutes(-10));

        await c.Llamar(entrante);
        var s = c.Settings;
        Assert.Equal(entrante, s.EstadoConexion);
        Assert.NotNull(s.DesconectadoDesde);
        Assert.False(c.Estado.VeniaConectadoAntesDeGracia);
        Assert.Equal(1, c.Emisiones);
        if (entrante == "DESCONECTADO") Assert.Null(s.NumeroVinculado);
        else Assert.Equal(NumeroFalso, s.NumeroVinculado);

        c.SimularTresMinutos();
        await c.Llamar(entrante);
        AfirmarUnSoloAdmin(c);
    }

    [Fact]
    public async Task BE_WA_GRA_010_IntegracionDeshabilitada_RegistraCaidaPeroNoNotifica()
    {
        var c = Crear("CONECTADO", CercaDelLimite(), habilitado: false);

        await c.Llamar(WhatsAppConstantes.ServicioNoDisponible);
        Assert.Equal("CONECTANDO", c.Settings.EstadoConexion);

        await Task.Delay(TimeSpan.FromSeconds(4));
        await c.Llamar(WhatsAppConstantes.ServicioNoDisponible);
        Assert.NotNull(c.Settings.DesconectadoDesde);

        c.SimularTresMinutos();
        await c.Llamar(WhatsAppConstantes.ServicioNoDisponible);
        Assert.Empty(c.Notifs);
        Assert.False(c.Settings.AvisoDesconexionEnviado);
    }

    private sealed class HubFalso : IHubContext<VentaInstantaneaHub>
    {
        public readonly List<(string Destino, string Metodo, object?[] Args)> Envios = new();
        public IHubClients Clients => new Clientes(this);
        public IGroupManager Groups => throw new NotSupportedException();
        private sealed class Clientes(HubFalso h) : IHubClients
        {
            public IClientProxy All => P("*");
            public IClientProxy AllExcept(IReadOnlyList<string> e) => P("*");
            public IClientProxy Client(string id) => P(id);
            public IClientProxy Clients(IReadOnlyList<string> ids) => P("*");
            public IClientProxy Group(string g) => P(g);
            public IClientProxy GroupExcept(string g, IReadOnlyList<string> e) => P(g);
            public IClientProxy Groups(IReadOnlyList<string> gs) => P("*");
            public IClientProxy User(string u) => P(u);
            public IClientProxy Users(IReadOnlyList<string> us) => P("*");
            private IClientProxy P(string d) => new Proxy(h, d);
        }
        private sealed class Proxy(HubFalso h, string d) : IClientProxy
        {
            public Task SendCoreAsync(string m, object?[] a, CancellationToken ct = default)
            { h.Envios.Add((d, m, a)); return Task.CompletedTask; }
        }
    }
}
