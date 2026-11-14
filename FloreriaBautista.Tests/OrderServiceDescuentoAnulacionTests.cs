using FloreriaBautista.Models.DTOs.Orders;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Enums;
using FloreriaBautista.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FloreriaBautista.Tests;

/// <summary>
/// FIX-001 (inventario-disponibilidad-ventas-prediccion): que un pedido normal ya descontó
/// su receta se deduce de la SALIDA de preparación escrita, no del estado anterior. Así el
/// camino por PENDIENTE_ANULACION descuenta y devuelve exactamente una vez.
/// </summary>
public class OrderServiceDescuentoAnulacionTests
{
    private static readonly DateTime Ahora  = new(2026, 10, 20, 18, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Manana = new(2026, 10, 21);

    private static (PedidosTestContext Ctx, Order Pedido, InventoryItem Insumo, Guid Admin) Montar()
    {
        var ctx = new PedidosTestContext(Ahora);
        var rol = new Role { Id = Guid.NewGuid(), Nombre = "ADMIN" };
        ctx.Db.Roles.Add(rol);
        var admin = ctx.AgregarEmpleado("Admin");
        ctx.Db.UserRoles.Add(new UserRole { UserId = admin, RoleId = rol.Id });

        var insumo = new InventoryItem
        {
            Id = Guid.NewGuid(), Nombre = "Rosa roja", StockActual = 100,
            StockMinimo = 1, Sucursal = "MATRIZ", UnidadMedida = "pza"
        };
        ctx.Db.InventoryItems.Add(insumo);
        var producto = ctx.AgregarProducto();
        ctx.Db.ProductRecipes.Add(new ProductRecipe
        {
            Id = Guid.NewGuid(), ProductId = producto.Id,
            InventoryItemId = insumo.Id, CantidadRequerida = 10
        });
        var pedido = ctx.AgregarPedido(Manana, EstadosPedido.PendienteValidacion);
        ctx.Db.OrderItems.Add(new OrderItem
        {
            Id = Guid.NewGuid(), OrderId = pedido.Id, ProductId = producto.Id,
            Cantidad = 1, PrecioUnitario = 350m, Subtotal = 350m
        });
        ctx.Db.SaveChanges();
        return (ctx, pedido, insumo, admin);
    }

    private static Task Cambiar(PedidosTestContext ctx, Guid pedidoId, Guid admin, string estado) =>
        ctx.CrearOrderService().CambiarEstadoAsync(pedidoId,
            new UpdateOrderStatusRequestDto
            {
                NuevoEstado = estado,
                MotivoCancelacion = estado == EstadosPedido.Cancelado ? "prueba" : null
            },
            ["ADMIN"], usuarioActor: admin);

    private static int Stock(PedidosTestContext ctx, Guid insumoId) =>
        ctx.Db.InventoryItems.AsNoTracking().Single(i => i.Id == insumoId).StockActual;

    private static int Movimientos(PedidosTestContext ctx, Guid insumoId, string tipo) =>
        ctx.Db.InventoryMovements.AsNoTracking()
            .Count(m => m.InventoryItemId == insumoId && m.TipoMovimiento == tipo);

    [Fact]
    public async Task Camino_A_Preparacion_Anulacion_Cancelado_Devuelve_Lo_Descontado()
    {
        var (ctx, pedido, insumo, admin) = Montar();
        using var _ = ctx;

        await Cambiar(ctx, pedido.Id, admin, EstadosPedido.EnPreparacion);
        Assert.Equal(90, Stock(ctx, insumo.Id));
        await Cambiar(ctx, pedido.Id, admin, EstadosPedido.PendienteAnulacion);
        await Cambiar(ctx, pedido.Id, admin, EstadosPedido.Cancelado);

        Assert.Equal(100, Stock(ctx, insumo.Id));
        Assert.Equal(1, Movimientos(ctx, insumo.Id, "SALIDA"));
        Assert.Equal(1, Movimientos(ctx, insumo.Id, "ENTRADA"));
    }

    [Fact]
    public async Task Camino_A_Anulacion_Rechazada_De_Vuelta_A_Preparacion_No_Descuenta_Dos_Veces()
    {
        var (ctx, pedido, insumo, admin) = Montar();
        using var _ = ctx;

        await Cambiar(ctx, pedido.Id, admin, EstadosPedido.EnPreparacion);
        await Cambiar(ctx, pedido.Id, admin, EstadosPedido.PendienteAnulacion);
        await Cambiar(ctx, pedido.Id, admin, EstadosPedido.EnPreparacion);

        Assert.Equal(90, Stock(ctx, insumo.Id));
        Assert.Equal(1, Movimientos(ctx, insumo.Id, "SALIDA"));
    }

    [Fact]
    public async Task Camino_B_Anulacion_Desde_Pendiente_Luego_Preparacion_Descuenta_Una_Vez_Y_Cancelar_Devuelve()
    {
        var (ctx, pedido, insumo, admin) = Montar();
        using var _ = ctx;

        await Cambiar(ctx, pedido.Id, admin, EstadosPedido.PendienteAnulacion);
        await Cambiar(ctx, pedido.Id, admin, EstadosPedido.EnPreparacion);

        Assert.Equal(90, Stock(ctx, insumo.Id));
        Assert.Equal(1, Movimientos(ctx, insumo.Id, "SALIDA"));

        await Cambiar(ctx, pedido.Id, admin, EstadosPedido.Cancelado);

        Assert.Equal(100, Stock(ctx, insumo.Id));
        Assert.Equal(1, Movimientos(ctx, insumo.Id, "ENTRADA"));
    }

    [Fact]
    public async Task Anulacion_Desde_Pendiente_Y_Cancelacion_No_Genera_Movimientos()
    {
        var (ctx, pedido, insumo, admin) = Montar();
        using var _ = ctx;

        await Cambiar(ctx, pedido.Id, admin, EstadosPedido.PendienteAnulacion);
        await Cambiar(ctx, pedido.Id, admin, EstadosPedido.Cancelado);

        Assert.Equal(100, Stock(ctx, insumo.Id));
        Assert.Equal(0, Movimientos(ctx, insumo.Id, "SALIDA"));
        Assert.Equal(0, Movimientos(ctx, insumo.Id, "ENTRADA"));
    }

    [Fact]
    public async Task Flujo_Normal_Preparacion_Ruta_Entrega_Descuenta_Una_Sola_Vez()
    {
        var (ctx, pedido, insumo, admin) = Montar();
        using var _ = ctx;

        await Cambiar(ctx, pedido.Id, admin, EstadosPedido.EnPreparacion);
        await Cambiar(ctx, pedido.Id, admin, EstadosPedido.EnRuta);
        await Cambiar(ctx, pedido.Id, admin, EstadosPedido.Entregado);

        Assert.Equal(90, Stock(ctx, insumo.Id));
        Assert.Equal(1, Movimientos(ctx, insumo.Id, "SALIDA"));
    }
}
