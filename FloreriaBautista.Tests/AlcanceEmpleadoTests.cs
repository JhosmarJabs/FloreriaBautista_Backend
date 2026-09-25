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
    public async Task El_Empleado_Ve_Sus_Ventas_De_Hoy_Y_Las_Entregas_De_Hoy_De_Otros()
    {
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var ana  = ctx.AgregarEmpleado("Ana");
        var beto = ctx.AgregarEmpleado("Beto");

        // Suya, capturada hoy, para entregar mañana (venta anticipada).
        var suya = ctx.AgregarPedido(Manana, EstadosPedido.EnPreparacion,
            atendidoPor: ana, fechaCreacion: Utc23HoraLocal);

        // De Beto, capturada días atrás, pero se entrega hoy: Ana tiene que poder
        // moverla aunque Beto no esté trabajando.
        var entregaDeHoy = ctx.AgregarPedido(Hoy, EstadosPedido.EnRuta,
            atendidoPor: beto, fechaCreacion: UtcAyer23HoraLocal);

        // De Beto, capturada hoy, para entregar mañana: esto NO le toca a Ana.
        ctx.AgregarPedido(Manana, EstadosPedido.EnPreparacion,
            atendidoPor: beto, fechaCreacion: Utc23HoraLocal);

        var resultado = await ctx.CrearOrderService().ListarEmpleadoAsync(ana, null, page: 1, size: 20);

        var ids = resultado.Items.Select(i => i.Id).ToList();
        Assert.Equal(2, ids.Count);
        Assert.Contains(suya.Id, ids);
        Assert.Contains(entregaDeHoy.Id, ids);
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
