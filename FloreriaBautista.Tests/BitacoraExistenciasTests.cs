using FloreriaBautista.Models.DTOs.Inventory;
using FloreriaBautista.Models.DTOs.Orders;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Enums;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Services;
using FloreriaBautista.Services.Notifications;
using FloreriaBautista.Services.WhatsApp;
using FloreriaBautista.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FloreriaBautista.Tests;

/// <summary>
/// bitacora-existencias-antes-despues: cada movimiento de inventario guarda las existencias
/// del insumo antes y después, encadenadas cuando un mismo insumo se mueve varias veces.
/// Los movimientos históricos quedan en null («sin dato»).
/// </summary>
public class BitacoraExistenciasTests
{
    private static readonly DateTime Ahora  = new(2026, 10, 20, 18, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Hoy    = new(2026, 10, 20);
    private static readonly DateOnly Manana = new(2026, 10, 21);

    private static InventoryItem NuevoInsumo(PedidosTestContext ctx, int stock, decimal factorMerma = 0m)
    {
        var insumo = new InventoryItem
        {
            Id = Guid.NewGuid(), Nombre = "Rosa roja", StockActual = stock, StockMinimo = 1,
            Sucursal = "MATRIZ", UnidadMedida = "pza", Activo = true, FactorMermaUso = factorMerma
        };
        ctx.Db.InventoryItems.Add(insumo);
        ctx.Db.SaveChanges();
        return insumo;
    }

    private static InventoryService CrearInventario(PedidosTestContext ctx) =>
        new(ctx.Db, NullLogger<InventoryService>.Instance, null!, new MemoryCache(new MemoryCacheOptions()),
            ctx.Fechas, new NotificationService(ctx.Db, NullLogger<NotificationService>.Instance),
            null!, new NullWhatsAppNotifier());

    private static Product ProductoConReceta(PedidosTestContext ctx, InventoryItem insumo, int cantidad)
    {
        var producto = ctx.AgregarProducto();
        ctx.Db.ProductRecipes.Add(new ProductRecipe
        {
            Id = Guid.NewGuid(), ProductId = producto.Id,
            InventoryItemId = insumo.Id, CantidadRequerida = cantidad
        });
        ctx.Db.SaveChanges();
        return producto;
    }

    private static List<InventoryMovement> Movs(PedidosTestContext ctx, Guid insumoId) =>
        ctx.Db.InventoryMovements.AsNoTracking()
            .Where(m => m.InventoryItemId == insumoId).OrderBy(m => m.FechaHora).ThenBy(m => m.Id).ToList();

    private static int StockActual(PedidosTestContext ctx, Guid insumoId) =>
        ctx.Db.InventoryItems.AsNoTracking().Single(i => i.Id == insumoId).StockActual;

    private static Order PedidoConLineas(PedidosTestContext ctx, string estado, params (Guid Producto, int Cantidad)[] lineas)
    {
        var pedido = ctx.AgregarPedido(Manana, estado);
        foreach (var (producto, cantidad) in lineas)
            ctx.Db.OrderItems.Add(new OrderItem
            {
                Id = Guid.NewGuid(), OrderId = pedido.Id, ProductId = producto,
                Cantidad = cantidad, PrecioUnitario = 350m, Subtotal = 350m * cantidad
            });
        ctx.Db.SaveChanges();
        return pedido;
    }

    // ── Movimiento manual ─────────────────────────────────────────

    [Fact]
    public async Task Entrada_Guarda_Antes_Y_Despues()
    {
        using var ctx = new PedidosTestContext(Ahora);
        var usuario = ctx.AgregarEmpleado("Admin");
        var insumo  = NuevoInsumo(ctx, 10);

        var dto = await CrearInventario(ctx).RegistrarMovimientoAsync(
            new RegisterMovementRequestDto { InventoryItemId = insumo.Id, Tipo = "ENTRADA", Cantidad = 5 }, usuario);

        Assert.Equal(10, dto.StockAntes);
        Assert.Equal(15, dto.StockDespues);
        var m = Assert.Single(Movs(ctx, insumo.Id));
        Assert.Equal(10, m.StockAntes);
        Assert.Equal(15, m.StockDespues);
        Assert.Equal(15, StockActual(ctx, insumo.Id));
    }

    [Fact]
    public async Task Salida_Con_Merma_Automatica_Encadena_Dos_Movimientos()
    {
        using var ctx = new PedidosTestContext(Ahora);
        var usuario = ctx.AgregarEmpleado("Admin");
        var insumo  = NuevoInsumo(ctx, 100, factorMerma: 0.10m);

        var dto = await CrearInventario(ctx).RegistrarMovimientoAsync(
            new RegisterMovementRequestDto { InventoryItemId = insumo.Id, Tipo = "SALIDA", Cantidad = 20 }, usuario);

        var movs = Movs(ctx, insumo.Id);
        Assert.Equal(2, movs.Count);
        var principal = movs.Single(m => m.MotivoCategoria == "OTRO");
        var merma     = movs.Single(m => m.MotivoCategoria == "MANIPULACION");
        Assert.Equal(100, principal.StockAntes);
        Assert.Equal(80,  principal.StockDespues);
        Assert.Equal(80,  merma.StockAntes);
        Assert.Equal(80 - merma.Cantidad, merma.StockDespues);
        Assert.Equal(merma.StockDespues, StockActual(ctx, insumo.Id));
        // La respuesta del POST conserva su contrato: existencias finales del insumo.
        Assert.Equal(100, dto.StockAntes);
        Assert.Equal(merma.StockDespues, dto.StockDespues);
    }

    [Fact]
    public async Task Ajuste_Guarda_El_Conteo_Como_Despues()
    {
        using var ctx = new PedidosTestContext(Ahora);
        var usuario = ctx.AgregarEmpleado("Admin");
        var insumo  = NuevoInsumo(ctx, 40);

        await CrearInventario(ctx).RegistrarMovimientoAsync(
            new RegisterMovementRequestDto { InventoryItemId = insumo.Id, Tipo = "AJUSTE", Cantidad = 25 }, usuario);

        var m = Assert.Single(Movs(ctx, insumo.Id));
        Assert.Equal(40, m.StockAntes);
        Assert.Equal(25, m.StockDespues);
    }

    [Fact]
    public async Task Salida_Mayor_Al_Stock_Se_Rechaza_Sin_Movimiento()
    {
        using var ctx = new PedidosTestContext(Ahora);
        var usuario = ctx.AgregarEmpleado("Admin");
        var insumo  = NuevoInsumo(ctx, 3);

        await Assert.ThrowsAsync<AppException>(() => CrearInventario(ctx).RegistrarMovimientoAsync(
            new RegisterMovementRequestDto { InventoryItemId = insumo.Id, Tipo = "SALIDA", Cantidad = 9 }, usuario));

        Assert.Empty(Movs(ctx, insumo.Id));
        Assert.Equal(3, StockActual(ctx, insumo.Id));
    }

    // ── Listado ───────────────────────────────────────────────────

    [Fact]
    public async Task Listado_Devuelve_Null_En_Historicos_Y_Valores_En_Nuevos()
    {
        using var ctx = new PedidosTestContext(Ahora);
        var usuario = ctx.AgregarEmpleado("Admin");
        var insumo  = NuevoInsumo(ctx, 10);
        ctx.Db.InventoryMovements.Add(new InventoryMovement
        {
            Id = Guid.NewGuid(), InventoryItemId = insumo.Id, TipoMovimiento = "SALIDA", Cantidad = 2,
            UsuarioId = usuario, FechaHora = Ahora.AddDays(-30)
        });
        ctx.Db.SaveChanges();
        var svc = CrearInventario(ctx);
        await svc.RegistrarMovimientoAsync(
            new RegisterMovementRequestDto { InventoryItemId = insumo.Id, Tipo = "ENTRADA", Cantidad = 4 }, usuario);

        var lista = (await svc.ListarMovimientosAsync(insumo.Id, 1, 10)).Items.ToList();

        Assert.Equal(2, lista.Count);
        var historico = lista.Single(m => m.Tipo == "SALIDA");
        Assert.Null(historico.StockAntes);
        Assert.Null(historico.StockDespues);
        var nuevo = lista.Single(m => m.Tipo == "ENTRADA");
        Assert.Equal(10, nuevo.StockAntes);
        Assert.Equal(14, nuevo.StockDespues);
    }

    // ── Pedidos ───────────────────────────────────────────────────

    [Fact]
    public async Task Venta_De_Mostrador_Con_Insumo_Repetido_Encadena_Los_Movimientos()
    {
        using var ctx = new PedidosTestContext(Ahora);
        var ana    = ctx.AgregarEmpleado("Ana");
        var insumo = NuevoInsumo(ctx, 100);
        var ramo   = ProductoConReceta(ctx, insumo, 10);
        var flor   = ProductoConReceta(ctx, insumo, 3);

        var venta = await ctx.CrearOrderService().CrearPedidoFisicoAsync(
            new CreatePhysicalOrderRequestDto
            {
                NombreCliente = "Cliente mostrador", FechaEntrega = Hoy, TipoPedido = "INSTANTANEO",
                Items = [new OrderItemRequestDto { ProductId = ramo.Id, Cantidad = 1 },
                         new OrderItemRequestDto { ProductId = flor.Id, Cantidad = 2 }],
                MontoPagado = 1050m, MetodoPago = "EFECTIVO"
            },
            atendidoPorUsuarioId: ana);

        // Misma FechaHora: se ordena por la cadena de existencias, no por el reloj.
        var movs = Movs(ctx, insumo.Id).Where(m => m.Motivo!.Contains(venta.Id.ToString()))
            .OrderByDescending(m => m.StockAntes).ToList();
        Assert.Equal(2, movs.Count);
        Assert.Equal(100, movs[0].StockAntes);
        Assert.Equal(movs[0].StockDespues, movs[1].StockAntes);
        Assert.Equal(movs[0].StockAntes - movs[0].Cantidad, movs[0].StockDespues);
        Assert.Equal(movs[1].StockAntes - movs[1].Cantidad, movs[1].StockDespues);
        Assert.Equal(StockActual(ctx, insumo.Id), movs[1].StockDespues);
        Assert.Equal(100 - 16, StockActual(ctx, insumo.Id));
    }

    [Fact]
    public async Task Preparacion_Y_Cancelacion_Guardan_Antes_Y_Despues()
    {
        using var ctx = new PedidosTestContext(Ahora);
        var admin    = ctx.AgregarEmpleado("Admin");
        var insumo   = NuevoInsumo(ctx, 50);
        var producto = ProductoConReceta(ctx, insumo, 10);
        var pedido   = PedidoConLineas(ctx, EstadosPedido.PendienteValidacion, (producto.Id, 1), (producto.Id, 2));

        await ctx.CrearOrderService().CambiarEstadoAsync(pedido.Id,
            new UpdateOrderStatusRequestDto { NuevoEstado = EstadosPedido.EnPreparacion }, ["ADMIN"], usuarioActor: admin);

        var salidas = Movs(ctx, insumo.Id).Where(m => m.TipoMovimiento == "SALIDA")
            .OrderByDescending(m => m.StockAntes).ToList();
        Assert.Equal(2, salidas.Count);
        Assert.Equal(50, salidas[0].StockAntes);
        Assert.Equal(salidas[0].StockDespues, salidas[1].StockAntes);
        Assert.Equal(StockActual(ctx, insumo.Id), salidas[1].StockDespues);
        Assert.Equal(20, StockActual(ctx, insumo.Id));

        await ctx.CrearOrderService().CambiarEstadoAsync(pedido.Id,
            new UpdateOrderStatusRequestDto { NuevoEstado = EstadosPedido.Cancelado, MotivoCancelacion = "prueba" },
            ["ADMIN"], usuarioActor: admin);

        var entradas = Movs(ctx, insumo.Id).Where(m => m.TipoMovimiento == "ENTRADA")
            .OrderBy(m => m.StockAntes).ToList();
        Assert.Equal(2, entradas.Count);
        Assert.Equal(20, entradas[0].StockAntes);
        Assert.Equal(entradas[0].StockAntes + entradas[0].Cantidad, entradas[0].StockDespues);
        Assert.Equal(entradas[0].StockDespues, entradas[1].StockAntes);
        Assert.Equal(50, entradas[1].StockDespues);
        Assert.Equal(50, StockActual(ctx, insumo.Id));
    }

    [Fact]
    public async Task Confirmacion_De_Pago_De_Venta_Instantanea_Guarda_Antes_Y_Despues()
    {
        using var ctx = new PedidosTestContext(Ahora, ignorarTransacciones: true);
        var admin    = ctx.AgregarEmpleado("Admin");
        var insumo   = NuevoInsumo(ctx, 60);
        var producto = ProductoConReceta(ctx, insumo, 10);
        var pedido   = PedidoConLineas(ctx, EstadosPedido.EnPreparacion, (producto.Id, 1));
        pedido.TipoPedido = "INSTANTANEO";
        pedido.SaldoPendiente = 350m;
        ctx.Db.SolicitudesVentaInstantanea.Add(new SolicitudVentaInstantanea
        {
            Id = Guid.NewGuid(), CustomerId = ctx.ClienteId, ProductId = producto.Id, Cantidad = 1,
            Estado = "ACEPTADA", OrderId = pedido.Id
        });
        ctx.Db.SaveChanges();

        await ctx.CrearOrderService().RegistrarPagoAsync(pedido.Id,
            new RegisterPaymentRequestDto { Monto = 350m, Metodo = "EFECTIVO" }, usuarioActor: admin);

        var m = Assert.Single(Movs(ctx, insumo.Id));
        Assert.Equal(60, m.StockAntes);
        Assert.Equal(50, m.StockDespues);
        Assert.Equal(50, StockActual(ctx, insumo.Id));
    }
}
