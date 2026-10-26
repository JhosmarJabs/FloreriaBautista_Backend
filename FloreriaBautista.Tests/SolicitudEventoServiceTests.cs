using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Eventos;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Services;
using FloreriaBautista.Services.Eventos;
using FloreriaBautista.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FloreriaBautista.Tests;

/// <summary>
/// Solicitudes de evento cotizables: validación, límite de 3 pendientes, aviso a los ADMIN,
/// paginación del listado y cambio de estado. InMemory no hace cumplir los CHECK de la BD:
/// esos los comprueba QA contra Neon.
/// </summary>
public class SolicitudEventoServiceTests
{
    // 2026-10-23 18:00 UTC = 12:00 hora de la tienda (UTC-6): "hoy" local es el 23.
    private static readonly DateTime AhoraUtc = new(2026, 10, 23, 18, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Hoy = new(2026, 10, 23);

    private static readonly TimeZoneInfo Zona = TimeZoneInfo.CreateCustomTimeZone(
        "Test-UTC-6", TimeSpan.FromHours(-6), "Prueba (UTC-6)", "Prueba (UTC-6)");

    private sealed class Escenario : IDisposable
    {
        public AppDbContext Db { get; }
        public SolicitudEventoService Servicio { get; }
        public Guid ClienteId { get; } = Guid.NewGuid();
        public Guid AdminId { get; } = Guid.NewGuid();
        public Guid EmpleadoId { get; } = Guid.NewGuid();

        public Escenario(INotificationService? notificaciones = null)
        {
            Db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"eventos-{Guid.NewGuid()}").Options);

            Db.Customers.Add(new Customer
            {
                Id = ClienteId, TipoCliente = "SESION", Nombre = "Ana", Apellido = "Lopez", Telefono = "5550000000"
            });
            var rolAdmin = new Role { Id = Guid.NewGuid(), Nombre = "ADMIN" };
            var rolEmpleado = new Role { Id = Guid.NewGuid(), Nombre = "EMPLEADO" };
            Db.Roles.AddRange(rolAdmin, rolEmpleado);
            Db.Users.AddRange(
                new User { Id = AdminId, Nombre = "Admin", Apellido = "Uno", Estado = "ACTIVO" },
                new User { Id = EmpleadoId, Nombre = "Emp", Apellido = "Dos", Estado = "ACTIVO" });
            Db.UserRoles.AddRange(
                new UserRole { UserId = AdminId, RoleId = rolAdmin.Id },
                new UserRole { UserId = EmpleadoId, RoleId = rolEmpleado.Id });
            Db.SaveChanges();

            Servicio = new SolicitudEventoService(
                Db,
                notificaciones ?? new NotificationService(Db, NullLogger<NotificationService>.Instance),
                new FechaHelper(Zona, () => AhoraUtc),
                NullLogger<SolicitudEventoService>.Instance);
        }

        public void Dispose() => Db.Dispose();
    }

    private sealed class NotificacionesQueFallan : INotificationService
    {
        public Task CrearParaUsuariosAsync(string tipo, string titulo, string mensaje, string[] roles,
            string? entidadTipo = null, Guid? entidadId = null) => throw new InvalidOperationException("caída simulada");
        public Task CrearParaUsuarioAsync(Guid usuarioId, string tipo, string titulo, string mensaje,
            string? entidadTipo = null, Guid? entidadId = null) => throw new NotImplementedException();
        public Task CrearParaClienteAsync(Guid customerId, string tipo, string titulo, string mensaje,
            string? entidadTipo = null, Guid? entidadId = null) => throw new NotImplementedException();
        public Task<FloreriaBautista.Models.DTOs.Common.PagedResultDto<FloreriaBautista.Models.DTOs.Notifications.NotificationDto>>
            ListarAsync(Guid? usuarioId, Guid? customerId, bool soloNoLeidas, int page, int size) => throw new NotImplementedException();
        public Task<int> ContarNoLeidasAsync(Guid? usuarioId, Guid? customerId) => throw new NotImplementedException();
        public Task MarcarLeidaAsync(Guid notificationId, Guid? usuarioId, Guid? customerId) => throw new NotImplementedException();
        public Task MarcarTodasLeidasAsync(Guid? usuarioId, Guid? customerId) => throw new NotImplementedException();
        public Task EliminarAsync(Guid notificationId, Guid? usuarioId, Guid? customerId) => throw new NotImplementedException();
        public Task<bool> ExisteNoLeidaRecienteAsync(string tipo, Guid? entidadId, TimeSpan ventana) => throw new NotImplementedException();
    }

    private static CrearSolicitudEventoDto Valida(Action<CrearSolicitudEventoDto>? ajustar = null)
    {
        var dto = new CrearSolicitudEventoDto
        {
            TipoEvento       = "boda",
            FechaEvento      = Hoy.AddDays(1),
            InvitadosAprox   = 120,
            NombreContacto   = "  QA-EVT Ana  ",
            TelefonoContacto = "+52 477-123-4567",
            CorreoContacto   = "ana@example.com",
            Mensaje          = "  Quiero cotizar  ",
        };
        ajustar?.Invoke(dto);
        return dto;
    }

    [Fact]
    public async Task Crear_Valida_Guarda_Pendiente_Con_El_Cliente_Dado_Y_Normaliza()
    {
        using var e = new Escenario();

        var res = await e.Servicio.CrearAsync(e.ClienteId, Valida());

        var fila = await e.Db.SolicitudesEvento.SingleAsync();
        Assert.Equal(e.ClienteId, fila.CustomerId);
        Assert.Equal("PENDIENTE", fila.Estado);
        Assert.Equal("BODA", fila.TipoEvento);
        Assert.Equal("QA-EVT Ana", fila.NombreContacto);
        Assert.Equal("4771234567", fila.TelefonoContacto);
        Assert.Equal("Quiero cotizar", fila.Mensaje);
        Assert.Equal(AhoraUtc, fila.CreadoEn);
        Assert.Equal(fila.CreadoEn, fila.ActualizadoEn);
        Assert.Equal(fila.Id, res.Id);
        Assert.Equal("Ana Lopez", res.ClienteNombre);
    }

    [Fact]
    public async Task Crear_Notifica_A_Cada_Admin_Activo_Y_A_Ningun_Empleado_Sin_Datos_De_Contacto()
    {
        using var e = new Escenario();

        var res = await e.Servicio.CrearAsync(e.ClienteId, Valida());

        var notas = await e.Db.Notifications.ToListAsync();
        var nota = Assert.Single(notas);
        Assert.Equal(e.AdminId, nota.DestinatarioUsuarioId);
        Assert.Equal("SOLICITUD_EVENTO", nota.Tipo);
        Assert.Equal("SolicitudEvento", nota.EntidadTipo);
        Assert.Equal(res.Id, nota.EntidadId);
        Assert.DoesNotContain(notas, n => n.DestinatarioUsuarioId == e.EmpleadoId);
        Assert.Contains("24/10/2026", nota.Mensaje);
        Assert.DoesNotContain("4771234567", nota.Mensaje);
        Assert.DoesNotContain("ana@example.com", nota.Mensaje);
    }

    [Fact]
    public async Task Crear_Si_La_Notificacion_Falla_La_Solicitud_Queda_Guardada()
    {
        using var e = new Escenario(new NotificacionesQueFallan());

        var res = await e.Servicio.CrearAsync(e.ClienteId, Valida());

        Assert.Equal(1, await e.Db.SolicitudesEvento.CountAsync());
        Assert.Equal("PENDIENTE", res.Estado);
    }

    [Theory]
    [InlineData(0)]   // hoy
    [InlineData(-3)]  // pasada
    public async Task Crear_Con_Fecha_Hoy_O_Pasada_Se_Rechaza(int dias)
    {
        using var e = new Escenario();
        await Assert.ThrowsAsync<AppException>(() =>
            e.Servicio.CrearAsync(e.ClienteId, Valida(d => d.FechaEvento = Hoy.AddDays(dias))));
        Assert.Equal(0, await e.Db.SolicitudesEvento.CountAsync());
        Assert.Equal(0, await e.Db.Notifications.CountAsync());
    }

    [Fact]
    public async Task Crear_Con_Fecha_A_Mas_De_Dos_Anios_Se_Rechaza()
    {
        using var e = new Escenario();
        await Assert.ThrowsAsync<AppException>(() =>
            e.Servicio.CrearAsync(e.ClienteId, Valida(d => d.FechaEvento = Hoy.AddYears(2).AddDays(1))));
    }

    [Fact]
    public async Task Crear_Con_Fecha_Exactamente_A_Dos_Anios_Se_Acepta()
    {
        using var e = new Escenario();
        await e.Servicio.CrearAsync(e.ClienteId, Valida(d => d.FechaEvento = Hoy.AddYears(2)));
        Assert.Equal(1, await e.Db.SolicitudesEvento.CountAsync());
    }

    [Fact]
    public async Task Crear_Con_Tipo_Fuera_De_Catalogo_Se_Rechaza()
    {
        using var e = new Escenario();
        await Assert.ThrowsAsync<AppException>(() =>
            e.Servicio.CrearAsync(e.ClienteId, Valida(d => d.TipoEvento = "FIESTA")));
        Assert.Equal(0, await e.Db.SolicitudesEvento.CountAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(5001)]
    public async Task Crear_Con_Invitados_Fuera_De_Rango_Se_Rechaza(int invitados)
    {
        using var e = new Escenario();
        await Assert.ThrowsAsync<AppException>(() =>
            e.Servicio.CrearAsync(e.ClienteId, Valida(d => d.InvitadosAprox = invitados)));
    }

    [Fact]
    public async Task Crear_Sin_Invitados_Es_Valido()
    {
        using var e = new Escenario();
        var res = await e.Servicio.CrearAsync(e.ClienteId, Valida(d => d.InvitadosAprox = null));
        Assert.Null(res.InvitadosAprox);
    }

    [Theory]
    [InlineData("123456789")]       // 9 dígitos
    [InlineData("47712345ab")]      // letras
    [InlineData("")]
    public async Task Crear_Con_Telefono_Invalido_Se_Rechaza(string telefono)
    {
        using var e = new Escenario();
        await Assert.ThrowsAsync<AppException>(() =>
            e.Servicio.CrearAsync(e.ClienteId, Valida(d => d.TelefonoContacto = telefono)));
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("A")]
    public async Task Crear_Con_Nombre_Vacio_O_Muy_Corto_Se_Rechaza(string nombre)
    {
        using var e = new Escenario();
        await Assert.ThrowsAsync<AppException>(() =>
            e.Servicio.CrearAsync(e.ClienteId, Valida(d => d.NombreContacto = nombre)));
    }

    [Fact]
    public async Task Crear_Con_Correo_Mal_Formado_O_Mensaje_Largo_Se_Rechaza()
    {
        using var e = new Escenario();
        await Assert.ThrowsAsync<AppException>(() =>
            e.Servicio.CrearAsync(e.ClienteId, Valida(d => d.CorreoContacto = "no-es-correo")));
        await Assert.ThrowsAsync<AppException>(() =>
            e.Servicio.CrearAsync(e.ClienteId, Valida(d => d.Mensaje = new string('x', 1001))));
        Assert.Equal(0, await e.Db.SolicitudesEvento.CountAsync());
    }

    [Fact]
    public async Task Crear_La_Cuarta_Pendiente_Se_Rechaza_Y_Se_Acepta_Tras_Pasar_Una_A_Contactada()
    {
        using var e = new Escenario();
        var primera = await e.Servicio.CrearAsync(e.ClienteId, Valida());
        await e.Servicio.CrearAsync(e.ClienteId, Valida());
        await e.Servicio.CrearAsync(e.ClienteId, Valida());

        var ex = await Assert.ThrowsAsync<AppException>(() => e.Servicio.CrearAsync(e.ClienteId, Valida()));
        Assert.Contains("3 solicitudes en espera", ex.Message);
        Assert.Equal(3, await e.Db.SolicitudesEvento.CountAsync());

        await e.Servicio.CambiarEstadoAsync(primera.Id, "CONTACTADA");
        await e.Servicio.CrearAsync(e.ClienteId, Valida());
        Assert.Equal(4, await e.Db.SolicitudesEvento.CountAsync());
    }

    [Fact]
    public async Task Listar_Limita_El_Tamano_A_100_Filtra_Por_Estado_Y_Ordena_Reciente_Primero()
    {
        using var e = new Escenario();
        for (var i = 0; i < 105; i++)
        {
            e.Db.SolicitudesEvento.Add(new SolicitudEvento
            {
                Id = Guid.NewGuid(), CustomerId = e.ClienteId, TipoEvento = "BODA", FechaEvento = Hoy.AddDays(5),
                NombreContacto = $"QA-EVT {i}", TelefonoContacto = "4771234567",
                Estado = i == 0 ? "CERRADA" : "PENDIENTE",
                CreadoEn = AhoraUtc.AddMinutes(i), ActualizadoEn = AhoraUtc.AddMinutes(i),
            });
        }
        await e.Db.SaveChangesAsync();

        var pagina = await e.Servicio.ListarAsync("pendiente", 1, 500);

        Assert.Equal(100, pagina.TamanoPagina);
        Assert.Equal(100, pagina.Items.Count);
        Assert.Equal(104, pagina.Total);
        Assert.All(pagina.Items, i => Assert.Equal("PENDIENTE", i.Estado));
        Assert.Equal("QA-EVT 104", pagina.Items[0].NombreContacto);
        Assert.True(pagina.Items.Zip(pagina.Items.Skip(1)).All(p => p.First.CreadoEn >= p.Second.CreadoEn));
    }

    [Fact]
    public async Task Listar_Con_Estado_Fuera_De_Catalogo_Se_Rechaza()
    {
        using var e = new Escenario();
        await Assert.ThrowsAsync<AppException>(() => e.Servicio.ListarAsync("ARCHIVADA", 1, 20));
    }

    [Fact]
    public async Task CambiarEstado_Actualiza_Estado_Y_ActualizadoEn()
    {
        using var e = new Escenario();
        var creada = await e.Servicio.CrearAsync(e.ClienteId, Valida());

        // Mismo escenario con el reloj adelantado: se verifica en la fila, no en el DTO.
        var fila = await e.Db.SolicitudesEvento.SingleAsync();
        fila.ActualizadoEn = AhoraUtc.AddHours(-1);
        await e.Db.SaveChangesAsync();

        var res = await e.Servicio.CambiarEstadoAsync(creada.Id, "contactada");

        Assert.Equal("CONTACTADA", res.Estado);
        Assert.True(res.ActualizadoEn > AhoraUtc.AddHours(-1));
    }

    [Fact]
    public async Task CambiarEstado_Invalido_Se_Rechaza_E_Id_Inexistente_Es_NoEncontrado()
    {
        using var e = new Escenario();
        var creada = await e.Servicio.CrearAsync(e.ClienteId, Valida());

        await Assert.ThrowsAsync<AppException>(() => e.Servicio.CambiarEstadoAsync(creada.Id, "ARCHIVADA"));
        await Assert.ThrowsAsync<NotFoundException>(() => e.Servicio.CambiarEstadoAsync(Guid.NewGuid(), "CERRADA"));
        Assert.Equal("PENDIENTE", (await e.Db.SolicitudesEvento.SingleAsync()).Estado);
    }
}
