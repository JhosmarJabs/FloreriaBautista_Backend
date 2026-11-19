using FloreriaBautista.Models.DTOs.Employee;
using FloreriaBautista.Models.DTOs.Orders;
using FloreriaBautista.Models.Enums;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Tests.TestHelpers;
using Xunit;

namespace FloreriaBautista.Tests;

/// <summary>
/// El aislamiento del empleado: solo lo suyo, solo hoy. Son las reglas que
/// justifican la app interna, así que se prueban contra el servicio y no contra
/// el controller — si mañana alguien añade una ruta nueva que llame al servicio,
/// hereda estas garantías sin tener que acordarse de ellas.
///
/// El reloj está clavado a las 23:00 hora de la florería a propósito: es la hora
/// en la que el día UTC ya cambió y el día local no, que es donde se rompen todas
/// las implementaciones que calculan "hoy" con DateTime.UtcNow.
/// </summary>
public class AlcanceEmpleadoTests
{
    // 11 de agosto de 2026, 23:00 en la florería (UTC-6) = 12 de agosto 05:00 UTC.
    private static readonly DateTime Utc23HoraLocal = new(2026, 8, 12, 5, 0, 0, DateTimeKind.Utc);

    // Mismo instante de reloj, un día antes: 10 de agosto 23:00 local.
    private static readonly DateTime UtcAyer23HoraLocal = new(2026, 8, 11, 5, 0, 0, DateTimeKind.Utc);

    private static readonly DateOnly Hoy    = new(2026, 8, 11);
    private static readonly DateOnly Manana = new(2026, 8, 12);

    // ── Gastos ────────────────────────────────────────────────────

    [Fact]
    public async Task Un_Empleado_No_Ve_Los_Gastos_De_Otro()
    {
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var ana  = ctx.AgregarEmpleado("Ana");
        var beto = ctx.AgregarEmpleado("Beto");

        ctx.AgregarGasto(ana,  Utc23HoraLocal, 100m, "Taxi de Ana");
        ctx.AgregarGasto(beto, Utc23HoraLocal, 250m, "Taxi de Beto");

        var gastosDeAna = await ctx.CrearGastoService().ListarDelDiaAsync(ana);

        Assert.Equal("Taxi de Ana", Assert.Single(gastosDeAna).Concepto);
    }

    [Fact]
    public async Task Los_Gastos_De_Ayer_No_Aparecen_Hoy()
    {
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var ana = ctx.AgregarEmpleado("Ana");

        ctx.AgregarGasto(ana, UtcAyer23HoraLocal, 100m, "Gasto de ayer");

        Assert.Empty(await ctx.CrearGastoService().ListarDelDiaAsync(ana));
    }

    [Fact]
    public async Task A_Las_23_Hora_Local_El_Gasto_De_Hoy_Sigue_Siendo_De_Hoy()
    {
        // La trampa: a esta hora DateTime.UtcNow ya está en el día 12, así que un
        // filtro calculado en UTC dejaría al empleado sin ver lo que acaba de
        // registrar, justo en las horas de más venta.
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var ana = ctx.AgregarEmpleado("Ana");

        ctx.AgregarGasto(ana, Utc23HoraLocal, 100m, "Gasto de las 23:00");

        Assert.Single(await ctx.CrearGastoService().ListarDelDiaAsync(ana));
    }

    [Fact]
    public async Task No_Se_Puede_Registrar_Un_Gasto_Despues_De_Cerrar_El_Corte()
    {
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var ana = ctx.AgregarEmpleado("Ana");

        await ctx.CrearCorteService().CerrarAsync(ana, new CreateCashCutRequestDto { EfectivoDeclarado = 0m });

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            ctx.CrearGastoService().RegistrarAsync(ana, new CreateExpenseRequestDto
            {
                Concepto = "Gasto tardío",
                Importe  = 50m
            }));

        Assert.Contains("corte de caja", ex.Message);
    }

    // ── Ventas ────────────────────────────────────────────────────

    [Fact]
    public async Task El_Empleado_Ve_Solo_Sus_Pedidos_Del_Dia()
    {
        // Decisión del usuario D-1 (2026-10-23): el empleado ve únicamente lo suyo.
        // Un pedido de otra persona que se entrega hoy lo atiende el administrador.
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var ana  = ctx.AgregarEmpleado("Ana");
        var beto = ctx.AgregarEmpleado("Beto");

        // Suya, capturada hoy, para entregar mañana (venta anticipada).
        var suya = ctx.AgregarPedido(Manana, EstadosPedido.EnPreparacion,
            atendidoPor: ana, fechaCreacion: Utc23HoraLocal);

        // De Beto, capturada días atrás, pero se entrega hoy: ya NO le toca a Ana.
        ctx.AgregarPedido(Hoy, EstadosPedido.EnRuta,
            atendidoPor: beto, fechaCreacion: UtcAyer23HoraLocal);

        // De Beto, capturada hoy, para entregar mañana: tampoco le toca a Ana.
        ctx.AgregarPedido(Manana, EstadosPedido.EnPreparacion,
            atendidoPor: beto, fechaCreacion: Utc23HoraLocal);

        var resultado = await ctx.CrearOrderService().ListarEmpleadoAsync(ana, null, page: 1, size: 20);

        var ids = resultado.Items.Select(i => i.Id).ToList();
        Assert.Equal(suya.Id, Assert.Single(ids));
    }

    [Fact]
    public async Task Un_Pedido_Ajeno_Pendiente_De_Validar_Capturado_Dias_Antes_No_Aparece_En_Listado_Ni_Delta()
    {
        // Escenario BUG-011: una cuenta nueva veía pedidos PENDIENTE_VALIDACION de
        // otra persona, capturados días antes, solo porque se entregan hoy.
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var ana  = ctx.AgregarEmpleado("Ana");
        var beto = ctx.AgregarEmpleado("Beto");

        var ajeno = ctx.AgregarPedido(Hoy, EstadosPedido.PendienteValidacion,
            atendidoPor: beto, fechaCreacion: UtcAyer23HoraLocal.AddDays(-2));

        var servicio = ctx.CrearOrderService();
        var listado  = await servicio.ListarEmpleadoAsync(ana, null, page: 1, size: 20);
        var delta    = await servicio.ListarDeltaEmpleadoAsync(ana, DesdeAntiguo);

        Assert.DoesNotContain(ajeno.Id, listado.Items.Select(i => i.Id));
        Assert.DoesNotContain(ajeno.Id, delta.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Un_Pedido_Sin_Dueno_Con_Entrega_Hoy_No_Es_Visible_Para_El_Empleado()
    {
        // Pedidos de la tienda en línea o por teléfono: nadie los atiende todavía;
        // los toma el administrador.
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var ana = ctx.AgregarEmpleado("Ana");

        var sinDueno = ctx.AgregarPedido(Hoy, EstadosPedido.EnPreparacion,
            atendidoPor: null, fechaCreacion: UtcAyer23HoraLocal);

        var servicio = ctx.CrearOrderService();
        var listado  = await servicio.ListarEmpleadoAsync(ana, null, page: 1, size: 20);

        Assert.DoesNotContain(sinDueno.Id, listado.Items.Select(i => i.Id));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            servicio.ObtenerParaEmpleadoAsync(ana, sinDueno.Id));
    }

    [Fact]
    public async Task El_Empleado_Ve_Y_Avanza_Su_Pedido_Anticipado_Capturado_Antes_Con_Entrega_Hoy()
    {
        // La venta anticipada propia sigue funcionando el día de la entrega.
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var ana = ctx.AgregarEmpleado("Ana");

        var suya = ctx.AgregarPedido(Hoy, EstadosPedido.EnPreparacion,
            atendidoPor: ana, fechaCreacion: UtcAyer23HoraLocal);

        var servicio = ctx.CrearOrderService();
        var listado  = await servicio.ListarEmpleadoAsync(ana, null, page: 1, size: 20);
        Assert.Equal(suya.Id, Assert.Single(listado.Items).Id);

        var detalle = await servicio.ObtenerParaEmpleadoAsync(ana, suya.Id);
        Assert.Equal(suya.Id, detalle.Id);

        var movido = await servicio.CambiarEstadoAsync(
            suya.Id,
            new UpdateOrderStatusRequestDto { NuevoEstado = EstadosPedido.EnRuta },
            ["EMPLEADO"],
            restringirAEmpleado: ana, usuarioActor: ana);

        Assert.Equal(EstadosPedido.EnRuta, movido.EstadoPedido);
    }

    [Fact]
    public async Task Un_Pedido_Propio_Capturado_Antes_Con_Entrega_Manana_No_Es_Visible()
    {
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var ana = ctx.AgregarEmpleado("Ana");

        var propioDeAyer = ctx.AgregarPedido(Manana, EstadosPedido.EnPreparacion,
            atendidoPor: ana, fechaCreacion: UtcAyer23HoraLocal);

        var servicio = ctx.CrearOrderService();
        var listado  = await servicio.ListarEmpleadoAsync(ana, null, page: 1, size: 20);

        Assert.DoesNotContain(propioDeAyer.Id, listado.Items.Select(i => i.Id));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            servicio.ObtenerParaEmpleadoAsync(ana, propioDeAyer.Id));
    }

    [Theory]
    [InlineData(EstadosPedido.PendienteValidacion)]
    [InlineData(EstadosPedido.EnPreparacion)]
    [InlineData(EstadosPedido.EnRuta)]
    [InlineData(EstadosPedido.Entregado)]
    public async Task El_Detalle_De_Un_Pedido_Ajeno_Con_Entrega_Hoy_Responde_No_Encontrado(string estado)
    {
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var ana  = ctx.AgregarEmpleado("Ana");
        var beto = ctx.AgregarEmpleado("Beto");

        var ajeno = ctx.AgregarPedido(Hoy, estado,
            atendidoPor: beto, fechaCreacion: UtcAyer23HoraLocal);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ctx.CrearOrderService().ObtenerParaEmpleadoAsync(ana, ajeno.Id));
    }

    [Fact]
    public async Task El_Empleado_No_Mueve_Ni_Cobra_Un_Pedido_Ajeno_Con_Entrega_Hoy()
    {
        // Sustituye la expectativa antigua de que un abono ajeno del día respondía 200:
        // ahora es 404 y no cambia el estado, los pagos ni el inventario.
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var ana  = ctx.AgregarEmpleado("Ana");
        var beto = ctx.AgregarEmpleado("Beto");

        var ajeno = ctx.AgregarPedido(Hoy, EstadosPedido.EnPreparacion,
            atendidoPor: beto, fechaCreacion: UtcAyer23HoraLocal);
        ajeno.SaldoPendiente = 500m;
        ctx.Db.SaveChanges();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ctx.CrearOrderService().CambiarEstadoAsync(
                ajeno.Id,
                new UpdateOrderStatusRequestDto { NuevoEstado = EstadosPedido.EnRuta },
                ["EMPLEADO"],
                restringirAEmpleado: ana, usuarioActor: ana));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ctx.CrearOrderService().RegistrarPagoAsync(
                ajeno.Id,
                new RegisterPaymentRequestDto { Monto = 50m, Metodo = "EFECTIVO" },
                restringirAEmpleado: ana, usuarioActor: ana));

        var recargado = ctx.Recargar(ajeno.Id);
        Assert.Equal(EstadosPedido.EnPreparacion, recargado.EstadoPedido);
        Assert.Equal(500m, recargado.SaldoPendiente);
        Assert.Empty(ctx.Db.Payments.ToList());
        Assert.Empty(ctx.Db.InventoryMovements.ToList());
    }

    [Fact]
    public async Task El_Detalle_De_Un_Pedido_Ajeno_Responde_No_Encontrado()
    {
        // 404 y no 403: confirmar que el id existe ya sería decirle a Ana algo
        // sobre el trabajo de Beto.
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var ana  = ctx.AgregarEmpleado("Ana");
        var beto = ctx.AgregarEmpleado("Beto");

        var ajeno = ctx.AgregarPedido(Manana, EstadosPedido.EnPreparacion,
            atendidoPor: beto, fechaCreacion: Utc23HoraLocal);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ctx.CrearOrderService().ObtenerParaEmpleadoAsync(ana, ajeno.Id));
    }

    [Fact]
    public async Task La_Venta_Fisica_Queda_Atribuida_A_Quien_La_Captura()
    {
        // Sin esta atribución el resto del alcance no existe: el empleado no
        // vería su propia venta y su corte contaría cero.
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var ana      = ctx.AgregarEmpleado("Ana");
        var producto = ctx.AgregarProducto(precio: 350m);

        var venta = await ctx.CrearOrderService().CrearPedidoFisicoAsync(
            new CreatePhysicalOrderRequestDto
            {
                NombreCliente = "Cliente mostrador",
                FechaEntrega  = Hoy,
                TipoPedido    = "INSTANTANEO",
                Items         = [new OrderItemRequestDto { ProductId = producto.Id, Cantidad = 1 }],
                MontoPagado   = 350m,
                MetodoPago    = "EFECTIVO"
            },
            atendidoPorUsuarioId: ana);

        Assert.Equal(ana, ctx.Db.Orders.Single(o => o.Id == venta.Id).AtendidoPorUsuarioId);

        // Y aparece tanto en su listado del día como en su corte.
        var listado = await ctx.CrearOrderService().ListarEmpleadoAsync(ana, null, page: 1, size: 20);
        Assert.Contains(venta.Id, listado.Items.Select(i => i.Id));

        var preview = await ctx.CrearCorteService().PreviewAsync(ana);
        Assert.Equal(1, preview.PedidosContados);
        Assert.Equal(350m, preview.TotalEfectivo);
    }

    [Fact]
    public async Task Un_Empleado_No_Puede_Mover_El_Pedido_De_Otro()
    {
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var ana  = ctx.AgregarEmpleado("Ana");
        var beto = ctx.AgregarEmpleado("Beto");

        // De Beto, capturado hoy, para entregar mañana: fuera del alcance de Ana.
        var ajeno = ctx.AgregarPedido(Manana, EstadosPedido.EnPreparacion,
            atendidoPor: beto, fechaCreacion: Utc23HoraLocal);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ctx.CrearOrderService().CambiarEstadoAsync(
                ajeno.Id,
                new UpdateOrderStatusRequestDto { NuevoEstado = EstadosPedido.EnRuta },
                ["EMPLEADO"],
                restringirAEmpleado: ana));
    }

    [Fact]
    public async Task El_Admin_Si_Puede_Mover_Cualquier_Pedido()
    {
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var beto  = ctx.AgregarEmpleado("Beto");
        var ajeno = ctx.AgregarPedido(Manana, EstadosPedido.EnPreparacion,
            atendidoPor: beto, fechaCreacion: Utc23HoraLocal);

        // restringirAEmpleado en null = sin restricción.
        var resultado = await ctx.CrearOrderService().CambiarEstadoAsync(
            ajeno.Id,
            new UpdateOrderStatusRequestDto { NuevoEstado = EstadosPedido.EnRuta },
            ["ADMIN"]);

        Assert.Equal(EstadosPedido.EnRuta, resultado.EstadoPedido);
    }

    // ── Delta de pedidos ──────────────────────────────────────────

    private static readonly DateTime DesdeAntiguo = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task El_Delta_Del_Empleado_Excluye_Pedidos_Ajenos_De_Otro_Dia_Y_Archivados()
    {
        // El delta es otra puerta a los mismos pedidos: no puede dar más que el
        // listado. Antes entregaba el histórico completo del negocio.
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var ana  = ctx.AgregarEmpleado("Ana");
        var beto = ctx.AgregarEmpleado("Beto");

        // De Beto, capturado hoy, para entregar mañana: no le toca a Ana.
        ctx.AgregarPedido(Manana, EstadosPedido.EnPreparacion,
            atendidoPor: beto, fechaCreacion: Utc23HoraLocal);
        // De Ana, pero de ayer y ya archivado.
        ctx.AgregarPedido(new DateOnly(2026, 8, 10), EstadosPedido.Entregado, archivado: true,
            atendidoPor: ana, fechaCreacion: UtcAyer23HoraLocal);

        var delta = await ctx.CrearOrderService().ListarDeltaEmpleadoAsync(ana, DesdeAntiguo);

        Assert.Empty(delta.Items);
        Assert.False(string.IsNullOrEmpty(delta.SincronizadoEn));
    }

    [Fact]
    public async Task El_Delta_Del_Empleado_Incluye_Su_Venta_De_Hoy_Y_No_La_Entrega_De_Hoy_De_Otro()
    {
        // Decisión del usuario D-1 (2026-10-23): el delta parte del mismo alcance
        // que el listado, así que tampoco trae el pedido ajeno que se entrega hoy.
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var ana  = ctx.AgregarEmpleado("Ana");
        var beto = ctx.AgregarEmpleado("Beto");

        var suya = ctx.AgregarPedido(Manana, EstadosPedido.EnPreparacion,
            atendidoPor: ana, fechaCreacion: Utc23HoraLocal);
        ctx.AgregarPedido(Hoy, EstadosPedido.EnRuta,
            atendidoPor: beto, fechaCreacion: UtcAyer23HoraLocal);

        var delta = await ctx.CrearOrderService().ListarDeltaEmpleadoAsync(ana, DesdeAntiguo);

        Assert.Equal(suya.Id, Assert.Single(delta.Items).Id);
    }

    [Fact]
    public async Task El_Delta_Del_Empleado_Respeta_La_Marca_Desde()
    {
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var ana  = ctx.AgregarEmpleado("Ana");
        var suya = ctx.AgregarPedido(Manana, EstadosPedido.EnPreparacion,
            atendidoPor: ana, fechaCreacion: Utc23HoraLocal);

        var delta = await ctx.CrearOrderService()
            .ListarDeltaEmpleadoAsync(ana, suya.ActualizadoEn.AddSeconds(1));

        Assert.Empty(delta.Items);
    }

    // ── Corte de caja ─────────────────────────────────────────────

    [Fact]
    public async Task El_Efectivo_Esperado_Descuenta_Los_Gastos_Del_Dia()
    {
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var ana = ctx.AgregarEmpleado("Ana");

        var pedido = ctx.AgregarPedido(Hoy, EstadosPedido.Entregado,
            atendidoPor: ana, fechaCreacion: Utc23HoraLocal, total: 800m);
        ctx.AgregarPagoEfectivo(pedido.Id, Utc23HoraLocal, 800m);
        ctx.AgregarGasto(ana, Utc23HoraLocal, 120m);

        var preview = await ctx.CrearCorteService().PreviewAsync(ana);

        Assert.Equal(800m, preview.TotalVentas);
        Assert.Equal(800m, preview.TotalEfectivo);
        Assert.Equal(120m, preview.TotalGastos);
        Assert.Equal(680m, preview.EfectivoEsperado);
        Assert.False(preview.YaCerrado);
    }

    [Fact]
    public async Task El_Corte_No_Cuenta_El_Efectivo_Cobrado_Por_Otro_Empleado()
    {
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var ana  = ctx.AgregarEmpleado("Ana");
        var beto = ctx.AgregarEmpleado("Beto");

        var deBeto = ctx.AgregarPedido(Hoy, EstadosPedido.Entregado,
            atendidoPor: beto, fechaCreacion: Utc23HoraLocal, total: 500m);
        ctx.AgregarPagoEfectivo(deBeto.Id, Utc23HoraLocal, 500m);

        var preview = await ctx.CrearCorteService().PreviewAsync(ana);

        Assert.Equal(0m, preview.TotalEfectivo);
        Assert.Equal(0, preview.PedidosContados);
    }

    [Fact]
    public async Task Cerrar_Dos_Veces_El_Mismo_Dia_Se_Rechaza()
    {
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var ana = ctx.AgregarEmpleado("Ana");
        var servicio = ctx.CrearCorteService();

        await servicio.CerrarAsync(ana, new CreateCashCutRequestDto { EfectivoDeclarado = 0m });

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            servicio.CerrarAsync(ana, new CreateCashCutRequestDto { EfectivoDeclarado = 0m }));

        Assert.Contains("Ya cerraste", ex.Message);
    }

    [Fact]
    public async Task El_Corte_Sella_Los_Gastos_Que_Conto()
    {
        // Sin el sello, un gasto en la frontera del día podría volver a restarse
        // en el corte siguiente.
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var ana    = ctx.AgregarEmpleado("Ana");
        var pedido = ctx.AgregarPedido(Hoy, EstadosPedido.Entregado,
            atendidoPor: ana, fechaCreacion: Utc23HoraLocal, total: 200m);
        ctx.AgregarPagoEfectivo(pedido.Id, Utc23HoraLocal, 200m);
        var gasto = ctx.AgregarGasto(ana, Utc23HoraLocal, 75m);

        // 200 cobrados − 75 gastados = 125 esperados en la caja.
        var corte = await ctx.CrearCorteService()
            .CerrarAsync(ana, new CreateCashCutRequestDto { EfectivoDeclarado = 125m });

        ctx.Db.Entry(gasto).Reload();
        Assert.Equal(corte.Id, gasto.CashCutId);
        Assert.Equal(125m, corte.EfectivoEsperado);
        Assert.Equal(0m,   corte.Diferencia);
    }

    // ── Reporte de errores ────────────────────────────────────────

    [Fact]
    public async Task No_Se_Puede_Reportar_Un_Registro_Ajeno()
    {
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var ana   = ctx.AgregarEmpleado("Ana");
        var beto  = ctx.AgregarEmpleado("Beto");
        var ajeno = ctx.AgregarGasto(beto, Utc23HoraLocal, 300m);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ctx.CrearReporteService().CrearAsync(ana, new CreateErrorReportRequestDto
            {
                TipoRegistro = "GASTO",
                RegistroId   = ajeno.Id,
                Motivo       = "Me equivoqué en el importe"
            }));
    }

    [Fact]
    public async Task El_Segundo_Reporte_Sobre_El_Mismo_Registro_Se_Rechaza()
    {
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var ana   = ctx.AgregarEmpleado("Ana");
        var gasto = ctx.AgregarGasto(ana, Utc23HoraLocal, 300m);
        var servicio = ctx.CrearReporteService();

        var peticion = new CreateErrorReportRequestDto
        {
            TipoRegistro = "GASTO",
            RegistroId   = gasto.Id,
            Motivo       = "Me equivoqué en el importe"
        };

        await servicio.CrearAsync(ana, peticion);

        var ex = await Assert.ThrowsAsync<AppException>(() => servicio.CrearAsync(ana, peticion));
        Assert.Contains("reporte abierto", ex.Message);
    }

    [Fact]
    public async Task Anular_Un_Gasto_Desde_El_Reporte_No_Borra_La_Fila()
    {
        // Un registro económico que desaparece descuadra cualquier corte que ya
        // lo contó, así que la anulación es un cambio de estado, no un DELETE.
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var ana   = ctx.AgregarEmpleado("Ana");
        var admin = ctx.AgregarEmpleado("Admin");
        var gasto = ctx.AgregarGasto(ana, Utc23HoraLocal, 300m);
        var servicio = ctx.CrearReporteService();

        var reporte = await servicio.CrearAsync(ana, new CreateErrorReportRequestDto
        {
            TipoRegistro = "GASTO",
            RegistroId   = gasto.Id,
            Motivo       = "El importe correcto era 30, no 300"
        });

        await servicio.ResolverAsync(reporte.Id, admin,
            new ResolveErrorReportRequestDto { Accion = "ELIMINADO" });

        ctx.Db.Entry(gasto).Reload();
        Assert.Equal("ANULADO", gasto.Estado);
        Assert.NotNull(ctx.Db.Expenses.Find(gasto.Id));
    }
}
