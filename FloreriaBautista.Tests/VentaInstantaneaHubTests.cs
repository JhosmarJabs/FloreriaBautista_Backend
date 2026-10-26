using System.Reflection;
using System.Security.Claims;
using FloreriaBautista.Data;
using FloreriaBautista.Hubs;
using FloreriaBautista.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FloreriaBautista.Tests;

/// <summary>
/// SuscribirSolicitud solo deja entrar al grupo de una solicitud a su dueño
/// (rol CLIENTE). Se prueba el hub directo con dobles manuales: el contexto de
/// la conexion y el gestor de grupos. Crea su propio contexto InMemory.
/// </summary>
public class VentaInstantaneaHubTests
{
    private static readonly Guid UserDueno = Guid.NewGuid();
    private static readonly Guid UserAjeno = Guid.NewGuid();
    private static readonly Guid SolicitudId = Guid.NewGuid();

    private static AppDbContext NuevoContexto()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("hub-" + Guid.NewGuid())
            .Options;
        var ctx = new AppDbContext(options);

        var dueno = new Customer { Id = Guid.NewGuid(), UserId = UserDueno, Nombre = "Dueno" };
        var ajeno = new Customer { Id = Guid.NewGuid(), UserId = UserAjeno, Nombre = "Ajeno" };
        ctx.Customers.AddRange(dueno, ajeno);
        ctx.SolicitudesVentaInstantanea.Add(new SolicitudVentaInstantanea
        {
            Id = SolicitudId,
            CustomerId = dueno.Id,
            ProductId = Guid.NewGuid(),
            Cantidad = 1,
        });
        ctx.SaveChanges();
        return ctx;
    }

    private static (VentaInstantaneaHub hub, GruposFalsos grupos) CrearHub(
        AppDbContext ctx, Guid? userId, params string[] roles)
    {
        var claims = new List<Claim>();
        if (userId.HasValue) claims.Add(new Claim("sub", userId.Value.ToString()));
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        var grupos = new GruposFalsos();
        var hub = new VentaInstantaneaHub(ctx, NullLogger<VentaInstantaneaHub>.Instance)
        {
            Context = new ContextoFalso(principal),
            Groups = grupos,
        };
        return (hub, grupos);
    }

    [Fact]
    public async Task Dueno_cliente_se_suscribe_al_grupo_de_su_solicitud()
    {
        using var ctx = NuevoContexto();
        var (hub, grupos) = CrearHub(ctx, UserDueno, "CLIENTE");

        await hub.SuscribirSolicitud(SolicitudId.ToString());

        var llamada = Assert.Single(grupos.Altas);
        Assert.Equal("conn-1", llamada.Conexion);
        Assert.Equal($"solicitud-{SolicitudId}", llamada.Grupo);
    }

    [Fact]
    public async Task Cliente_ajeno_es_rechazado_sin_entrar_al_grupo()
    {
        using var ctx = NuevoContexto();
        var (hub, grupos) = CrearHub(ctx, UserAjeno, "CLIENTE");

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.SuscribirSolicitud(SolicitudId.ToString()));

        Assert.Equal("No autorizado.", ex.Message);
        Assert.Empty(grupos.Altas);
    }

    [Fact]
    public async Task Solicitud_inexistente_da_el_mismo_mensaje_que_la_ajena()
    {
        using var ctx = NuevoContexto();
        var (hub, grupos) = CrearHub(ctx, UserDueno, "CLIENTE");

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.SuscribirSolicitud(Guid.NewGuid().ToString()));

        Assert.Equal("No autorizado.", ex.Message);
        Assert.Empty(grupos.Altas);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Id_con_formato_invalido_da_solicitud_no_valida(string? id)
    {
        using var ctx = NuevoContexto();
        var (hub, grupos) = CrearHub(ctx, UserDueno, "CLIENTE");

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.SuscribirSolicitud(id!));

        Assert.Equal("Solicitud no válida.", ex.Message);
        Assert.Empty(grupos.Altas);
    }

    [Theory]
    [InlineData("EMPLEADO")]
    [InlineData("ADMIN")]
    public async Task Personal_no_puede_suscribirse_aunque_la_solicitud_exista(string rol)
    {
        using var ctx = NuevoContexto();
        var (hub, grupos) = CrearHub(ctx, UserDueno, rol);

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.SuscribirSolicitud(SolicitudId.ToString()));

        Assert.Equal("No autorizado.", ex.Message);
        Assert.Empty(grupos.Altas);
    }

    [Fact]
    public async Task Id_en_mayusculas_entra_al_grupo_en_minusculas()
    {
        using var ctx = NuevoContexto();
        var (hub, grupos) = CrearHub(ctx, UserDueno, "CLIENTE");

        await hub.SuscribirSolicitud(SolicitudId.ToString().ToUpperInvariant());

        var llamada = Assert.Single(grupos.Altas);
        Assert.Equal($"solicitud-{SolicitudId.ToString().ToLowerInvariant()}", llamada.Grupo);
    }

    [Fact]
    public async Task Principal_sin_sub_es_rechazado()
    {
        using var ctx = NuevoContexto();
        var (hub, grupos) = CrearHub(ctx, null, "CLIENTE");

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.SuscribirSolicitud(SolicitudId.ToString()));

        Assert.Equal("No autorizado.", ex.Message);
        Assert.Empty(grupos.Altas);
    }

    [Fact]
    public void Hub_conserva_Authorize_sin_roles()
    {
        var atributo = typeof(VentaInstantaneaHub).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(atributo);
        Assert.Null(atributo!.Roles);
    }

    private sealed class GruposFalsos : IGroupManager
    {
        public List<(string Conexion, string Grupo)> Altas { get; } = new();

        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            Altas.Add((connectionId, groupName));
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class ContextoFalso : HubCallerContext
    {
        private readonly ClaimsPrincipal _user;
        public ContextoFalso(ClaimsPrincipal user) => _user = user;

        public override string ConnectionId => "conn-1";
        public override string? UserIdentifier => null;
        public override ClaimsPrincipal? User => _user;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort() { }
    }
}
