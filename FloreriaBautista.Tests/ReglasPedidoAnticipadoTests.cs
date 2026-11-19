using FloreriaBautista.Models.DTOs.Orders;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Enums;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Xunit;

namespace FloreriaBautista.Tests;

/// <summary>
/// Reglas de servidor de los pedidos anticipados: solo el admin cancela o anula,
/// un abono con la misma clave no se registra dos veces, la fecha de entrega no
/// puede ser pasada y el efectivo cuenta en el corte de quien lo cobró.
/// </summary>
public class ReglasPedidoAnticipadoTests
{
    // 20 de octubre de 2026, 12:00 en la florería (UTC-6).
    private static readonly DateTime Mediodia = new(2026, 10, 20, 18, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Hoy      = new(2026, 10, 20);
    private static readonly DateOnly Ayer     = new(2026, 10, 19);
    private static readonly DateOnly Manana   = new(2026, 10, 21);

    private const string IndiceIdLocalPago = "ix_payments_id_local_offline";

    private static UpdateOrderStatusRequestDto Estado(string nuevo) =>
        new() { NuevoEstado = nuevo, MotivoCancelacion = "Prueba" };

    private static RegisterPaymentRequestDto Abono(decimal monto, Guid? clave, string metodo = "TARJETA") =>
        new() { Monto = monto, Metodo = metodo, IdLocalOffline = clave };

    private static Order PedidoConSaldo(PedidosTestContext ctx, decimal saldo, string estado = EstadosPedido.EnPreparacion,
                                        Guid? atendidoPor = null)
    {
        var pedido = ctx.AgregarPedido(Manana, estado, atendidoPor: atendidoPor, fechaCreacion: Mediodia);
        pedido.SaldoPendiente = saldo;
        ctx.Db.SaveChanges();
        return pedido;
    }

    // ── Cancelación y anulación solo para ADMIN ───────────────────

    [Theory]
    [InlineData(EstadosPedido.EnPreparacion, EstadosPedido.Cancelado)]
    [InlineData(EstadosPedido.PendienteValidacion, EstadosPedido.PendienteAnulacion)]
    [InlineData(EstadosPedido.PendienteAnulacion, EstadosPedido.EnPreparacion)]
    [InlineData(EstadosPedido.PendienteAnulacion, EstadosPedido.Cancelado)]
    [InlineData(EstadosPedido.Entregado, EstadosPedido.Cancelado)]
    public async Task El_Empleado_No_Cancela_Ni_Anula(string actual, string nuevo)
    {
        using var ctx = new PedidosTestContext(Mediodia);
        var ana    = ctx.AgregarEmpleado("Ana");
        var pedido = ctx.AgregarPedido(Hoy, actual, atendidoPor: ana, fechaCreacion: Mediodia);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            ctx.CrearOrderService().CambiarEstadoAsync(pedido.Id, Estado(nuevo), ["EMPLEADO"],
                restringirAEmpleado: ana, usuarioActor: ana));

        Assert.Equal(actual, ctx.Recargar(pedido.Id).EstadoPedido);
        Assert.Empty(ctx.Db.InventoryMovements.AsNoTracking().ToList());
    }

    [Fact]
    public async Task Fuera_De_Su_Alcance_El_Empleado_Sigue_Recibiendo_404()
    {
        using var ctx = new PedidosTestContext(Mediodia);
        var ana  = ctx.AgregarEmpleado("Ana");
        var beto = ctx.AgregarEmpleado("Beto");
        // De Beto, capturado ayer, para entregar mañana: no es del día de Ana.
        var ajeno = ctx.AgregarPedido(Manana, EstadosPedido.EnPreparacion,
            atendidoPor: beto, fechaCreacion: Mediodia.AddDays(-1));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ctx.CrearOrderService().CambiarEstadoAsync(ajeno.Id, Estado(EstadosPedido.Cancelado),
                ["EMPLEADO"], restringirAEmpleado: ana));
    }

    [Fact]
    public async Task El_Admin_Cancela_Y_Resuelve_Anulaciones_Como_Antes()
    {
        using var ctx = new PedidosTestContext(Mediodia);
        var admin    = ctx.AgregarEmpleado("Admin");
        var cancelar = ctx.AgregarPedido(Manana, EstadosPedido.EnPreparacion);
        var anulado  = ctx.AgregarPedido(Manana, EstadosPedido.PendienteAnulacion);

        var r1 = await ctx.CrearOrderService().CambiarEstadoAsync(cancelar.Id,
            Estado(EstadosPedido.Cancelado), ["admin"], usuarioActor: admin);
        var r2 = await ctx.CrearOrderService().CambiarEstadoAsync(anulado.Id,
            Estado(EstadosPedido.EnPreparacion), ["EMPLEADO", "ADMIN"], usuarioActor: admin);

        Assert.Equal(EstadosPedido.Cancelado, r1.EstadoPedido);
        Assert.Equal(EstadosPedido.EnPreparacion, r2.EstadoPedido);
    }

    [Fact]
    public async Task El_Empleado_Sigue_Avanzando_El_Pedido()
    {
        using var ctx = new PedidosTestContext(Mediodia);
        var ana    = ctx.AgregarEmpleado("Ana");
        var pedido = ctx.AgregarPedido(Hoy, EstadosPedido.EnPreparacion, atendidoPor: ana, fechaCreacion: Mediodia);

        var resultado = await ctx.CrearOrderService().CambiarEstadoAsync(pedido.Id,
            new UpdateOrderStatusRequestDto { NuevoEstado = EstadosPedido.EnRuta }, ["EMPLEADO"],
            restringirAEmpleado: ana, usuarioActor: ana);

        Assert.Equal(EstadosPedido.EnRuta, resultado.EstadoPedido);
    }

    // ── Abonos idempotentes y quién cobró ─────────────────────────

    [Fact]
    public async Task El_Mismo_Abono_Dos_Veces_Se_Registra_Una_Sola_Vez()
    {
        using var ctx = new PedidosTestContext(Mediodia, ignorarTransacciones: true);
        var ana    = ctx.AgregarEmpleado("Ana");
        var pedido = PedidoConSaldo(ctx, 300m);
        var clave  = Guid.NewGuid();

        var primera = await ctx.CrearOrderService().RegistrarPagoAsync(pedido.Id, Abono(100m, clave), usuarioActor: ana);
        var reenvio = await ctx.CrearOrderService().RegistrarPagoAsync(pedido.Id, Abono(100m, clave), usuarioActor: ana);

        Assert.Equal(200m, primera.SaldoPendiente);
        Assert.Equal(200m, reenvio.SaldoPendiente);
        var pago = Assert.Single(ctx.Db.Payments.AsNoTracking().Where(p => p.OrderId == pedido.Id).ToList());
        Assert.Equal(clave, pago.IdLocalOffline);
        Assert.Equal(ana, pago.RegistradoPorUsuarioId);
        Assert.Equal(200m, ctx.Recargar(pedido.Id).SaldoPendiente);
    }

    [Fact]
    public async Task Reintentar_Una_Liquidacion_No_Falla_Por_Saldo()
    {
        using var ctx = new PedidosTestContext(Mediodia, ignorarTransacciones: true);
        var pedido = PedidoConSaldo(ctx, 200m);
        var clave  = Guid.NewGuid();

        await ctx.CrearOrderService().RegistrarPagoAsync(pedido.Id, Abono(200m, clave));
        var reenvio = await ctx.CrearOrderService().RegistrarPagoAsync(pedido.Id, Abono(200m, clave));

        Assert.Equal(0m, reenvio.SaldoPendiente);
        var pago = Assert.Single(ctx.Db.Payments.AsNoTracking().Where(p => p.OrderId == pedido.Id).ToList());
        Assert.Equal("LIQUIDACION", pago.TipoPago);
    }

    [Fact]
    public async Task La_Misma_Clave_En_Otro_Pedido_Responde_Conflicto()
    {
        using var ctx = new PedidosTestContext(Mediodia, ignorarTransacciones: true);
        var a     = PedidoConSaldo(ctx, 300m);
        var b     = PedidoConSaldo(ctx, 300m);
        var clave = Guid.NewGuid();

        await ctx.CrearOrderService().RegistrarPagoAsync(a.Id, Abono(100m, clave));

        await Assert.ThrowsAsync<ConflictException>(() =>
            ctx.CrearOrderService().RegistrarPagoAsync(b.Id, Abono(100m, clave)));

        Assert.Equal(300m, ctx.Recargar(b.Id).SaldoPendiente);
        Assert.Empty(ctx.Db.Payments.AsNoTracking().Where(p => p.OrderId == b.Id).ToList());
    }

    [Fact]
    public async Task Sin_Clave_Dos_Abonos_Son_Dos_Pagos()
    {
        using var ctx = new PedidosTestContext(Mediodia, ignorarTransacciones: true);
        var pedido = PedidoConSaldo(ctx, 300m);

        await ctx.CrearOrderService().RegistrarPagoAsync(pedido.Id, Abono(100m, null));
        await ctx.CrearOrderService().RegistrarPagoAsync(pedido.Id, Abono(100m, Guid.Empty));

        var pagos = ctx.Db.Payments.AsNoTracking().Where(p => p.OrderId == pedido.Id).ToList();
        Assert.Equal(2, pagos.Count);
        Assert.All(pagos, p => Assert.Null(p.IdLocalOffline));
        Assert.All(pagos, p => Assert.Null(p.RegistradoPorUsuarioId));
        Assert.Equal(100m, ctx.Recargar(pedido.Id).SaldoPendiente);
    }

    [Fact]
    public async Task La_Colision_Simultanea_Devuelve_El_Pedido_Sin_Un_Segundo_Pago()
    {
        var clave     = Guid.NewGuid();
        var colision  = new ColisionAlGuardarPago(new PostgresException(
            "duplicate key value violates unique constraint", "ERROR", "ERROR",
            PostgresErrorCodes.UniqueViolation, constraintName: IndiceIdLocalPago));
        using var ctx = new PedidosTestContext(Mediodia, ignorarTransacciones: true, colision);
        var pedido = PedidoConSaldo(ctx, 300m);

        // La "otra petición" gana la carrera: guarda el abono y descuenta el saldo.
        colision.OtraPeticion = async () =>
        {
            await using var bd = ctx.NuevoContexto();
            bd.Payments.Add(new Payment
            {
                Id = Guid.NewGuid(), OrderId = pedido.Id, Monto = 100m, TipoPago = "ANTICIPO",
                Metodo = "TARJETA", Estado = "REGISTRADO", FechaPago = Mediodia, IdLocalOffline = clave
            });
            // Solo se actualiza el saldo, sin releer el pedido en este contexto.
            var guardado = new Order { Id = pedido.Id, SaldoPendiente = 200m };
            bd.Orders.Attach(guardado).Property(o => o.SaldoPendiente).IsModified = true;
            await bd.SaveChangesAsync();
        };

        var respuesta = await ctx.CrearOrderService().RegistrarPagoAsync(pedido.Id, Abono(100m, clave));

        Assert.True(colision.Disparada);
        Assert.Equal(200m, respuesta.SaldoPendiente);
        using var lectura = ctx.NuevoContexto();
        Assert.Single(lectura.Payments.Where(p => p.OrderId == pedido.Id));
    }

    // ── Fecha de entrega no pasada ────────────────────────────────

    private static CreatePhysicalOrderRequestDto Anticipada(Guid productoId, DateOnly fecha, Guid? idLocal = null,
                                                            string tipo = "ANTICIPADO") => new()
    {
        NombreCliente  = "Cliente con fecha",
        FechaEntrega   = fecha,
        TipoPedido     = tipo,
        Items          = [new OrderItemRequestDto { ProductId = productoId, Cantidad = 1 }],
        IdLocalOffline = idLocal,
        Direccion      = new DireccionDto { Calle = "Av. Juárez 120", Colonia = "Centro", Municipio = "Huejutla", Estado = "Hidalgo" }
    };

    [Fact]
    public async Task Un_Anticipado_Con_Fecha_De_Ayer_Se_Rechaza_Sin_Crear_Nada()
    {
        using var ctx = new PedidosTestContext(Mediodia);
        var ana      = ctx.AgregarEmpleado("Ana");
        var producto = ctx.AgregarProducto();
        var clientes = ctx.Db.Customers.Count();

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            ctx.CrearOrderService().CrearPedidoFisicoAsync(Anticipada(producto.Id, Ayer), ana));

        Assert.Equal("La fecha de entrega no puede ser anterior a hoy.", ex.Message);
        Assert.Empty(ctx.Db.Orders.AsNoTracking().ToList());
        Assert.Equal(clientes, ctx.Db.Customers.Count());
    }

    [Fact]
    public async Task Un_Anticipado_Con_Fecha_De_Hoy_Se_Registra()
    {
        using var ctx = new PedidosTestContext(Mediodia);
        var ana      = ctx.AgregarEmpleado("Ana");
        var producto = ctx.AgregarProducto();

        var pedido = await ctx.CrearOrderService().CrearPedidoFisicoAsync(Anticipada(producto.Id, Hoy), ana);

        Assert.Equal(EstadosPedido.PendienteValidacion, pedido.EstadoPedido);
    }

    [Fact]
    public async Task Hoy_Es_El_De_La_Tienda_Y_No_El_De_UTC()
    {
        // 23:00 en la tienda del 20; en UTC ya es el 21.
        using var ctx = new PedidosTestContext(new DateTime(2026, 10, 21, 5, 0, 0, DateTimeKind.Utc));
        var ana      = ctx.AgregarEmpleado("Ana");
        var producto = ctx.AgregarProducto();

        var pedido = await ctx.CrearOrderService().CrearPedidoFisicoAsync(Anticipada(producto.Id, Hoy), ana);

        Assert.Equal(Hoy, pedido.FechaEntrega);
    }

    [Fact]
    public async Task Una_Venta_De_Mostrador_Con_Fecha_Pasada_Se_Acepta_Como_Antes()
    {
        using var ctx = new PedidosTestContext(Mediodia);
        var ana      = ctx.AgregarEmpleado("Ana");
        var producto = ctx.AgregarProducto();
        var venta    = Anticipada(producto.Id, Ayer, tipo: "INSTANTANEO");
        venta.Direccion   = null;
        venta.MontoPagado = 350m;
        venta.MetodoPago  = "EFECTIVO";

        var pedido = await ctx.CrearOrderService().CrearPedidoFisicoAsync(venta, ana);

        Assert.Equal(EstadosPedido.Entregado, pedido.EstadoPedido);
        var pago = Assert.Single(ctx.Db.Payments.AsNoTracking().Where(p => p.OrderId == pedido.Id).ToList());
        Assert.Equal(ana, pago.RegistradoPorUsuarioId);
    }

    [Fact]
    public async Task El_Reenvio_De_Un_Anticipado_Ya_Creado_Se_Devuelve_Aunque_Su_Fecha_Ya_Paso()
    {
        using var ctx = new PedidosTestContext(Mediodia);
        var ana      = ctx.AgregarEmpleado("Ana");
        var producto = ctx.AgregarProducto();
        var x        = Guid.NewGuid();

        var original = await ctx.CrearOrderService().CrearPedidoFisicoAsync(Anticipada(producto.Id, Hoy, x), ana);
        ctx.AvanzarRelojA(Mediodia.AddDays(1));

        var reenvio = await ctx.CrearOrderService().CrearPedidoFisicoAsync(Anticipada(producto.Id, Hoy, x), ana);

        Assert.Equal(original.Id, reenvio.Id);
    }

    [Fact]
    public async Task El_Cliente_Web_Tampoco_Programa_Un_Anticipado_En_El_Pasado()
    {
        using var ctx = new PedidosTestContext(Mediodia);
        var usuario  = ctx.AgregarEmpleado("Cliente web");
        var producto = ctx.AgregarProducto();
        var cliente  = ctx.Db.Customers.Single(c => c.Id == ctx.ClienteId);
        cliente.UserId = usuario;
        ctx.Db.SaveChanges();

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            ctx.CrearOrderService().CrearPedidoClienteAsync(usuario, new CreateOrderRequestDto
            {
                FechaEntrega = Ayer,
                TipoPedido   = "ANTICIPADO",
                Items        = [new OrderItemRequestDto { ProductId = producto.Id, Cantidad = 1 }],
                Direccion    = new DireccionDto { Calle = "Calle 1", Colonia = "Centro", Municipio = "Huejutla", Estado = "Hidalgo" }
            }));

        Assert.Equal("La fecha de entrega no puede ser anterior a hoy.", ex.Message);
        Assert.Empty(ctx.Db.Orders.AsNoTracking().ToList());
    }

    // ── Corte de caja por quien cobró ─────────────────────────────

    [Fact]
    public async Task El_Efectivo_Cuenta_En_El_Corte_De_Quien_Lo_Cobro()
    {
        using var ctx = new PedidosTestContext(Mediodia, ignorarTransacciones: true);
        var ana  = ctx.AgregarEmpleado("Ana");
        var beto = ctx.AgregarEmpleado("Beto");
        var deAna = PedidoConSaldo(ctx, 300m, atendidoPor: ana);

        await ctx.CrearOrderService().RegistrarPagoAsync(deAna.Id, Abono(150m, Guid.NewGuid(), "EFECTIVO"),
            usuarioActor: beto);

        var corteBeto = await ctx.CrearCorteService().PreviewAsync(beto);
        var corteAna  = await ctx.CrearCorteService().PreviewAsync(ana);

        Assert.Equal(150m, corteBeto.TotalEfectivo);
        Assert.Equal(0m, corteAna.TotalEfectivo);
    }

    [Fact]
    public async Task El_Efectivo_Que_Cobra_El_Admin_No_Entra_Al_Corte_Del_Empleado()
    {
        using var ctx = new PedidosTestContext(Mediodia, ignorarTransacciones: true);
        var ana   = ctx.AgregarEmpleado("Ana");
        var admin = ctx.AgregarEmpleado("Admin");
        var deAna = PedidoConSaldo(ctx, 300m, atendidoPor: ana);

        await ctx.CrearOrderService().RegistrarPagoAsync(deAna.Id, Abono(100m, null, "EFECTIVO"), usuarioActor: admin);

        Assert.Equal(0m, (await ctx.CrearCorteService().PreviewAsync(ana)).TotalEfectivo);
    }

    // Simula la carrera: justo antes del INSERT del abono, "otra petición" guarda
    // el mismo y el guardado falla como lo haría PostgreSQL.
    private sealed class ColisionAlGuardarPago(PostgresException innerException) : SaveChangesInterceptor
    {
        public Func<Task> OtraPeticion { get; set; } = () => Task.CompletedTask;
        public bool       Disparada    { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var guardaAbono = eventData.Context!.ChangeTracker.Entries<Payment>()
                .Any(e => e.State == EntityState.Added && e.Entity.IdLocalOffline.HasValue);

            if (Disparada || !guardaAbono)
                return result;

            Disparada = true;
            await OtraPeticion();
            throw new DbUpdateException("Guardado fallido simulado.", innerException);
        }
    }
}
