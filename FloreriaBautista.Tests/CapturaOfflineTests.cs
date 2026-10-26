using FloreriaBautista.Models.DTOs.Employee;
using FloreriaBautista.Models.DTOs.Orders;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Xunit;

namespace FloreriaBautista.Tests;

/// <summary>
/// Ventas y gastos capturados sin conexión que llegan después por la cola de la
/// app móvil (plan pwa-offline-gps, B-back-1 y B-back-2):
/// - la venta cuenta en el corte del día de captura si sigue abierto; si cerró,
///   entra al corte vigente y conserva CapturadoEn;
/// - el gasto se fecha con su captura y se rechaza si el corte de ese día cerró;
/// - ventana de 48 h y tolerancia de reloj de 5 min;
/// - un total distinto al vigente se rechaza (409) y avisa a los administradores;
/// - el gasto es idempotente por IdLocalOffline y por usuario.
/// </summary>
public class CapturaOfflineTests
{
    // 11 de agosto de 2026, 12:00 en la florería (UTC-6).
    private static readonly DateTime Mediodia = new(2026, 8, 11, 18, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Hoy      = new(2026, 8, 11);
    private static readonly DateOnly Ayer     = new(2026, 8, 10);

    // 10 de agosto, 15:00 locales: 27 h antes de Mediodia, dentro de la ventana.
    private static readonly DateTime CapturaDeAyer = new(2026, 8, 10, 21, 0, 0, DateTimeKind.Utc);

    private static CreatePhysicalOrderRequestDto Venta(Guid productoId, DateTime? capturadoEn = null,
        decimal? totalCapturado = null, Guid? idLocalOffline = null) => new()
    {
        NombreCliente  = "Cliente mostrador",
        FechaEntrega   = Hoy,
        TipoPedido     = "INSTANTANEO",
        Items          = [new OrderItemRequestDto { ProductId = productoId, Cantidad = 1 }],
        MontoPagado    = 350m,
        MetodoPago     = "EFECTIVO",
        IdLocalOffline = idLocalOffline ?? Guid.NewGuid(),
        CapturadoEn    = capturadoEn,
        TotalCapturado = totalCapturado
    };

    private static CreateExpenseRequestDto Gasto(Guid? idLocalOffline = null, DateTime? capturadoEn = null) => new()
    {
        Concepto       = "Taxi",
        Importe        = 80m,
        IdLocalOffline = idLocalOffline,
        CapturadoEn    = capturadoEn
    };

    // ── Ventas: regla del corte ───────────────────────────────────

    [Fact]
    public async Task Venta_De_Ayer_Con_El_Corte_De_Ayer_Abierto_Cuenta_En_Ayer()
    {
        using var ctx = new PedidosTestContext(Mediodia);
        var ana      = ctx.AgregarEmpleado("Ana");
        var producto = ctx.AgregarProducto(precio: 350m);

        var venta = await ctx.CrearOrderService()
            .CrearPedidoFisicoAsync(Venta(producto.Id, CapturaDeAyer, 350m), ana);

        Assert.Equal(CapturaDeAyer, venta.FechaCreacion);
        Assert.Equal(CapturaDeAyer, venta.CapturadoEn);
        Assert.Equal(CapturaDeAyer, ctx.Db.Payments.Single(p => p.OrderId == venta.Id).FechaPago);
    }

    [Fact]
    public async Task Venta_De_Ayer_Con_El_Corte_De_Ayer_Cerrado_Entra_Al_Corte_Vigente()
    {
        using var ctx = new PedidosTestContext(Mediodia);
        var ana      = ctx.AgregarEmpleado("Ana");
        var producto = ctx.AgregarProducto(precio: 350m);
        AgregarCorte(ctx, ana, Ayer);

        var venta = await ctx.CrearOrderService()
            .CrearPedidoFisicoAsync(Venta(producto.Id, CapturaDeAyer, 350m), ana);

        Assert.Equal(Mediodia, venta.FechaCreacion);
        Assert.Equal(CapturaDeAyer, venta.CapturadoEn);
        Assert.Equal(Mediodia, ctx.Db.Payments.Single(p => p.OrderId == venta.Id).FechaPago);
    }

    [Fact]
    public async Task El_Corte_Anulado_No_Cuenta_Como_Cerrado()
    {
        using var ctx = new PedidosTestContext(Mediodia);
        var ana      = ctx.AgregarEmpleado("Ana");
        var producto = ctx.AgregarProducto(precio: 350m);
        AgregarCorte(ctx, ana, Ayer, estado: "ANULADO");

        var venta = await ctx.CrearOrderService()
            .CrearPedidoFisicoAsync(Venta(producto.Id, CapturaDeAyer), ana);

        Assert.Equal(CapturaDeAyer, venta.FechaCreacion);
    }

    [Fact]
    public async Task El_Corte_Cerrado_De_Otro_Empleado_No_Afecta()
    {
        using var ctx = new PedidosTestContext(Mediodia);
        var ana      = ctx.AgregarEmpleado("Ana");
        var beto     = ctx.AgregarEmpleado("Beto");
        var producto = ctx.AgregarProducto(precio: 350m);
        AgregarCorte(ctx, beto, Ayer);

        var venta = await ctx.CrearOrderService()
            .CrearPedidoFisicoAsync(Venta(producto.Id, CapturaDeAyer), ana);

        Assert.Equal(CapturaDeAyer, venta.FechaCreacion);
    }

    [Fact]
    public async Task Venta_Sin_Datos_De_Captura_Se_Comporta_Como_Antes()
    {
        using var ctx = new PedidosTestContext(Mediodia);
        var ana      = ctx.AgregarEmpleado("Ana");
        var producto = ctx.AgregarProducto(precio: 350m);

        var venta = await ctx.CrearOrderService().CrearPedidoFisicoAsync(Venta(producto.Id), ana);

        Assert.Equal(Mediodia, venta.FechaCreacion);
        Assert.Null(venta.CapturadoEn);
    }

    // ── Ventas: ventana de captura ────────────────────────────────

    [Fact]
    public async Task Venta_Capturada_Hace_Mas_De_48_Horas_Se_Rechaza_Sin_Crear_Pedido()
    {
        using var ctx = new PedidosTestContext(Mediodia);
        var ana      = ctx.AgregarEmpleado("Ana");
        var producto = ctx.AgregarProducto(precio: 350m);

        await Assert.ThrowsAsync<AppException>(() => ctx.CrearOrderService()
            .CrearPedidoFisicoAsync(Venta(producto.Id, Mediodia.AddHours(-49)), ana));

        Assert.Empty(ctx.Db.Orders.Where(o => o.Canal == "FISICO"));
    }

    [Fact]
    public async Task Venta_Capturada_En_El_Futuro_Se_Rechaza()
    {
        using var ctx = new PedidosTestContext(Mediodia);
        var ana      = ctx.AgregarEmpleado("Ana");
        var producto = ctx.AgregarProducto(precio: 350m);

        await Assert.ThrowsAsync<AppException>(() => ctx.CrearOrderService()
            .CrearPedidoFisicoAsync(Venta(producto.Id, Mediodia.AddMinutes(10)), ana));
    }

    [Fact]
    public async Task Un_Desfase_De_Reloj_Pequeno_Se_Tolera()
    {
        using var ctx = new PedidosTestContext(Mediodia);
        var ana      = ctx.AgregarEmpleado("Ana");
        var producto = ctx.AgregarProducto(precio: 350m);

        var venta = await ctx.CrearOrderService()
            .CrearPedidoFisicoAsync(Venta(producto.Id, Mediodia.AddMinutes(2)), ana);

        Assert.Equal(Mediodia.AddMinutes(2), venta.CapturadoEn);
    }

    [Fact]
    public async Task El_Reenvio_De_Una_Venta_Ya_Registrada_Se_Devuelve_Aunque_Salga_De_La_Ventana()
    {
        using var ctx = new PedidosTestContext(Mediodia);
        var ana      = ctx.AgregarEmpleado("Ana");
        var producto = ctx.AgregarProducto(precio: 350m);
        var x        = Guid.NewGuid();

        var primera = await ctx.CrearOrderService()
            .CrearPedidoFisicoAsync(Venta(producto.Id, CapturaDeAyer, idLocalOffline: x), ana);

        ctx.AvanzarRelojA(Mediodia.AddDays(2));
        var reenvio = await ctx.CrearOrderService()
            .CrearPedidoFisicoAsync(Venta(producto.Id, CapturaDeAyer, idLocalOffline: x), ana);

        Assert.Equal(primera.Id, reenvio.Id);
    }

    // ── Ventas: total capturado ───────────────────────────────────

    [Fact]
    public async Task Venta_Con_Total_Distinto_Al_Vigente_Responde_Conflicto_Y_Avisa_Al_Admin()
    {
        using var ctx = new PedidosTestContext(Mediodia);
        var ana      = ctx.AgregarEmpleado("Ana");
        var admin    = AgregarAdmin(ctx);
        var producto = ctx.AgregarProducto(precio: 400m);   // el precio subió tras la captura

        var ex = await Assert.ThrowsAsync<ConflictException>(() => ctx.CrearOrderService()
            .CrearPedidoFisicoAsync(Venta(producto.Id, CapturaDeAyer, totalCapturado: 350m), ana));

        Assert.Contains("350.00", ex.Message);
        Assert.Contains("400.00", ex.Message);
        Assert.Empty(ctx.Db.Orders.Where(o => o.Canal == "FISICO"));
        var aviso = Assert.Single(ctx.Db.Notifications.Where(n => n.Tipo == "VENTA_REQUIERE_ATENCION"));
        Assert.Equal(admin, aviso.DestinatarioUsuarioId);
    }

    [Fact]
    public async Task Venta_Con_Total_Igual_Al_Vigente_Se_Registra()
    {
        using var ctx = new PedidosTestContext(Mediodia);
        var ana      = ctx.AgregarEmpleado("Ana");
        var producto = ctx.AgregarProducto(precio: 350m);

        var venta = await ctx.CrearOrderService()
            .CrearPedidoFisicoAsync(Venta(producto.Id, CapturaDeAyer, totalCapturado: 350.00m), ana);

        Assert.Equal(350m, venta.Total);
        Assert.Empty(ctx.Db.Notifications);
    }

    // ── Gastos: fecha de captura y corte ──────────────────────────

    [Fact]
    public async Task Gasto_De_Ayer_Con_El_Corte_De_Ayer_Abierto_Se_Fecha_Con_Su_Captura()
    {
        using var ctx = new PedidosTestContext(Mediodia);
        var ana = ctx.AgregarEmpleado("Ana");

        var gasto = await ctx.CrearGastoService()
            .RegistrarAsync(ana, Gasto(Guid.NewGuid(), CapturaDeAyer));

        Assert.Equal(CapturaDeAyer, gasto.FechaHora);
        Assert.Equal(CapturaDeAyer, gasto.CapturadoEn);
    }

    [Fact]
    public async Task Gasto_De_Ayer_Con_El_Corte_De_Ayer_Cerrado_Se_Rechaza()
    {
        using var ctx = new PedidosTestContext(Mediodia);
        var ana = ctx.AgregarEmpleado("Ana");
        AgregarCorte(ctx, ana, Ayer);

        var ex = await Assert.ThrowsAsync<AppException>(() => ctx.CrearGastoService()
            .RegistrarAsync(ana, Gasto(Guid.NewGuid(), CapturaDeAyer)));

        Assert.Contains("Repórtalo al administrador", ex.Message);
        Assert.Empty(ctx.Db.Expenses);
    }

    [Fact]
    public async Task Gasto_De_Ayer_Se_Acepta_Aunque_El_Corte_De_Hoy_Este_Cerrado()
    {
        using var ctx = new PedidosTestContext(Mediodia);
        var ana = ctx.AgregarEmpleado("Ana");
        AgregarCorte(ctx, ana, Hoy);

        var gasto = await ctx.CrearGastoService()
            .RegistrarAsync(ana, Gasto(Guid.NewGuid(), CapturaDeAyer));

        Assert.Equal(CapturaDeAyer, gasto.FechaHora);
    }

    [Fact]
    public async Task Gasto_Capturado_Hace_Mas_De_48_Horas_Se_Rechaza()
    {
        using var ctx = new PedidosTestContext(Mediodia);
        var ana = ctx.AgregarEmpleado("Ana");

        await Assert.ThrowsAsync<AppException>(() => ctx.CrearGastoService()
            .RegistrarAsync(ana, Gasto(Guid.NewGuid(), Mediodia.AddHours(-49))));

        Assert.Empty(ctx.Db.Expenses);
    }

    [Fact]
    public async Task Gasto_Sin_Datos_De_Captura_Sigue_Rechazandose_Con_El_Corte_De_Hoy_Cerrado()
    {
        using var ctx = new PedidosTestContext(Mediodia);
        var ana = ctx.AgregarEmpleado("Ana");
        AgregarCorte(ctx, ana, Hoy);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            ctx.CrearGastoService().RegistrarAsync(ana, Gasto()));

        Assert.Contains("corte de caja de hoy", ex.Message);
    }

    // ── Gastos: idempotencia ──────────────────────────────────────

    [Fact]
    public async Task El_Reenvio_Del_Mismo_Gasto_Devuelve_El_Mismo_Registro()
    {
        using var ctx = new PedidosTestContext(Mediodia);
        var ana = ctx.AgregarEmpleado("Ana");
        var x   = Guid.NewGuid();

        var primero = await ctx.CrearGastoService().RegistrarAsync(ana, Gasto(x, CapturaDeAyer));
        var reenvio = await ctx.CrearGastoService().RegistrarAsync(ana, Gasto(x, CapturaDeAyer));

        Assert.Equal(primero.Id, reenvio.Id);
        Assert.Single(ctx.Db.Expenses.Where(e => e.IdLocalOffline == x));
    }

    [Fact]
    public async Task El_Reenvio_Se_Devuelve_Aunque_El_Corte_Ya_Se_Haya_Cerrado()
    {
        using var ctx = new PedidosTestContext(Mediodia);
        var ana = ctx.AgregarEmpleado("Ana");
        var x   = Guid.NewGuid();

        var primero = await ctx.CrearGastoService().RegistrarAsync(ana, Gasto(x));
        AgregarCorte(ctx, ana, Hoy);
        var reenvio = await ctx.CrearGastoService().RegistrarAsync(ana, Gasto(x));

        Assert.Equal(primero.Id, reenvio.Id);
    }

    [Fact]
    public async Task El_Gasto_De_Otro_Empleado_Responde_Conflicto_Sin_Exponerlo()
    {
        using var ctx = new PedidosTestContext(Mediodia);
        var ana  = ctx.AgregarEmpleado("Ana");
        var beto = ctx.AgregarEmpleado("Beto");
        var x    = Guid.NewGuid();

        await ctx.CrearGastoService().RegistrarAsync(ana, Gasto(x));

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            ctx.CrearGastoService().RegistrarAsync(beto, Gasto(x)));

        Assert.DoesNotContain("Taxi", ex.Message);
        Assert.Single(ctx.Db.Expenses.Where(e => e.IdLocalOffline == x));
    }

    [Fact]
    public async Task La_Colision_Simultanea_De_Gastos_Devuelve_El_Ya_Guardado()
    {
        var x         = Guid.NewGuid();
        var colision  = new ColisionAlGuardarGasto(new PostgresException(
            "duplicate key value violates unique constraint", "ERROR", "ERROR",
            PostgresErrorCodes.UniqueViolation, constraintName: "ix_expenses_id_local_offline"));
        using var ctx = new PedidosTestContext(Mediodia, colision);
        var ana     = ctx.AgregarEmpleado("Ana");
        var ganador = Guid.NewGuid();

        colision.OtraPeticion = async () =>
        {
            using var otra = ctx.NuevoContexto();
            otra.Expenses.Add(new Expense
            {
                Id = ganador, UsuarioId = ana, FechaHora = Mediodia, Concepto = "Taxi",
                Importe = 80m, IdLocalOffline = x
            });
            await otra.SaveChangesAsync();
        };

        var respuesta = await ctx.CrearGastoService().RegistrarAsync(ana, Gasto(x));

        Assert.True(colision.Disparada);
        Assert.Equal(ganador, respuesta.Id);
        using var bd = ctx.NuevoContexto();
        Assert.Single(bd.Expenses.Where(e => e.IdLocalOffline == x));
    }

    [Fact]
    public async Task Otro_Error_Al_Guardar_El_Gasto_Se_Propaga()
    {
        var colision  = new ColisionAlGuardarGasto(new PostgresException(
            "simulado", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation,
            constraintName: "ix_expenses_otra_restriccion"));
        using var ctx = new PedidosTestContext(Mediodia, colision);
        var ana = ctx.AgregarEmpleado("Ana");

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            ctx.CrearGastoService().RegistrarAsync(ana, Gasto(Guid.NewGuid())));
    }

    // ── Andamiaje ─────────────────────────────────────────────────

    private static void AgregarCorte(PedidosTestContext ctx, Guid usuarioId, DateOnly dia, string estado = "CERRADO")
    {
        ctx.Db.CashCuts.Add(new CashCut
        {
            Id               = Guid.NewGuid(),
            UsuarioId        = usuarioId,
            FechaLocal       = dia,
            FechaHoraCierre  = ctx.Fechas.InicioDelDiaUtc(dia).AddHours(20),
            PeriodoInicioUtc = ctx.Fechas.InicioDelDiaUtc(dia),
            PeriodoFinUtc    = ctx.Fechas.InicioDelDiaUtc(dia.AddDays(1)),
            Estado           = estado
        });
        ctx.Db.SaveChanges();
    }

    private static Guid AgregarAdmin(PedidosTestContext ctx)
    {
        var id  = ctx.AgregarEmpleado("Administradora");
        var rol = new Role { Id = Guid.NewGuid(), Nombre = "ADMIN" };
        ctx.Db.Roles.Add(rol);
        ctx.Db.UserRoles.Add(new UserRole { UserId = id, RoleId = rol.Id });
        ctx.Db.SaveChanges();
        return id;
    }

    /// <summary>Al primer guardado de un gasto nuevo con IdLocalOffline, ejecuta la
    /// "otra petición" y hace fallar el guardado como lo haría PostgreSQL.</summary>
    private sealed class ColisionAlGuardarGasto(PostgresException innerException) : SaveChangesInterceptor
    {
        public Func<Task> OtraPeticion { get; set; } = () => Task.CompletedTask;
        public bool       Disparada    { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var guardaGastoOffline = eventData.Context!.ChangeTracker.Entries<Expense>()
                .Any(e => e.State == EntityState.Added && e.Entity.IdLocalOffline.HasValue);

            if (Disparada || !guardaGastoOffline)
                return result;

            Disparada = true;
            await OtraPeticion();
            throw new DbUpdateException("Guardado fallido simulado.", innerException);
        }
    }
}
