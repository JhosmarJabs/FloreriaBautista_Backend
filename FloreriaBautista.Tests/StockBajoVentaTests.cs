using FloreriaBautista.Models.DTOs.Orders;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Enums;
using FloreriaBautista.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FloreriaBautista.Tests;

/// <summary>
/// FIX-004 (plataforma-v2-04-notificaciones): una salida por venta que hace cruzar el
/// mínimo de un insumo genera el aviso STOCK_BAJO, con la misma regla que el movimiento
/// manual (solo al cruzar y suprimido si ya hay uno sin leer de las últimas 24 h).
/// </summary>
public class StockBajoVentaTests
{
    private static readonly DateTime Ahora  = new(2026, 10, 20, 18, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Manana = new(2026, 10, 21);

    private static Guid AgregarAdminActivo(PedidosTestContext ctx)
    {
        var rol = ctx.Db.Roles.FirstOrDefault(r => r.Nombre == "ADMIN");
        if (rol == null)
        {
            rol = new Role { Id = Guid.NewGuid(), Nombre = "ADMIN" };
            ctx.Db.Roles.Add(rol);
        }
        var id = ctx.AgregarEmpleado("Admin");
        ctx.Db.UserRoles.Add(new UserRole { UserId = id, RoleId = rol.Id });
        ctx.Db.SaveChanges();
        return id;
    }

    /// <summary>Pedido pendiente de validar cuyo producto gasta 3 unidades de un insumo.</summary>
    private static (Order Pedido, InventoryItem Insumo) PedidoConReceta(
        PedidosTestContext ctx, int stockInicial, int minimo)
    {
        var insumo = new InventoryItem
        {
            Id = Guid.NewGuid(), Nombre = "Rosa roja", StockActual = stockInicial,
            StockMinimo = minimo, Sucursal = "MATRIZ", UnidadMedida = "pza"
        };
        ctx.Db.InventoryItems.Add(insumo);
        var producto = ctx.AgregarProducto();
        ctx.Db.ProductRecipes.Add(new ProductRecipe
        {
            Id = Guid.NewGuid(), ProductId = producto.Id,
            InventoryItemId = insumo.Id, CantidadRequerida = 3
        });
        var pedido = ctx.AgregarPedido(Manana, EstadosPedido.PendienteValidacion);
        ctx.Db.OrderItems.Add(new OrderItem
        {
            Id = Guid.NewGuid(), OrderId = pedido.Id, ProductId = producto.Id,
            Cantidad = 1, PrecioUnitario = 350m, Subtotal = 350m
        });
        ctx.Db.SaveChanges();
        return (pedido, insumo);
    }

    private static Task Preparar(PedidosTestContext ctx, Guid pedidoId, Guid admin) =>
        ctx.CrearOrderService().CambiarEstadoAsync(pedidoId,
            new UpdateOrderStatusRequestDto { NuevoEstado = EstadosPedido.EnPreparacion },
            ["ADMIN"], usuarioActor: admin);

    private static List<Notification> Avisos(PedidosTestContext ctx, Guid insumoId) =>
        ctx.Db.Notifications.AsNoTracking()
            .Where(n => n.Tipo == "STOCK_BAJO" && n.EntidadId == insumoId).ToList();

    [Fact]
    public async Task Preparar_Pedido_Que_Cruza_El_Minimo_Crea_STOCK_BAJO()
    {
        using var ctx = new PedidosTestContext(Ahora);
        var admin = AgregarAdminActivo(ctx);
        var (pedido, insumo) = PedidoConReceta(ctx, stockInicial: 8, minimo: 5);

        await Preparar(ctx, pedido.Id, admin);

        var aviso = Assert.Single(Avisos(ctx, insumo.Id));
        Assert.Equal(admin, aviso.DestinatarioUsuarioId);
        Assert.Equal("InventoryItem", aviso.EntidadTipo);
        Assert.Equal("Stock bajo: Rosa roja", aviso.Titulo);
        Assert.Equal("Rosa roja bajó a 5 pza (mínimo: 5).", aviso.Mensaje);
    }

    [Fact]
    public async Task Preparar_Pedido_Que_No_Cruza_El_Minimo_No_Avisa()
    {
        using var ctx = new PedidosTestContext(Ahora);
        var admin = AgregarAdminActivo(ctx);
        var (pedido, insumo) = PedidoConReceta(ctx, stockInicial: 20, minimo: 5);

        await Preparar(ctx, pedido.Id, admin);

        Assert.Empty(Avisos(ctx, insumo.Id));
    }

    [Fact]
    public async Task Preparar_Pedido_Con_Insumo_Ya_Bajo_Minimo_No_Avisa_De_Nuevo()
    {
        using var ctx = new PedidosTestContext(Ahora);
        var admin = AgregarAdminActivo(ctx);
        var (pedido, insumo) = PedidoConReceta(ctx, stockInicial: 4, minimo: 5);

        await Preparar(ctx, pedido.Id, admin);

        Assert.Empty(Avisos(ctx, insumo.Id));
    }

    [Fact]
    public async Task Preparar_Pedido_Con_Aviso_Sin_Leer_Reciente_Lo_Suprime()
    {
        using var ctx = new PedidosTestContext(Ahora);
        var admin = AgregarAdminActivo(ctx);
        var (pedido, insumo) = PedidoConReceta(ctx, stockInicial: 8, minimo: 5);
        ctx.Db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(), DestinatarioUsuarioId = admin, Tipo = "STOCK_BAJO",
            Titulo = "previo", Mensaje = "previo", EntidadTipo = "InventoryItem",
            EntidadId = insumo.Id, CreadaEn = DateTime.UtcNow
        });
        ctx.Db.SaveChanges();

        await Preparar(ctx, pedido.Id, admin);

        Assert.Single(Avisos(ctx, insumo.Id));
    }

    [Fact]
    public async Task Cancelar_Pedido_En_Preparacion_Devuelve_Stock_Sin_Avisar()
    {
        using var ctx = new PedidosTestContext(Ahora);
        var admin = AgregarAdminActivo(ctx);
        var (pedido, insumo) = PedidoConReceta(ctx, stockInicial: 20, minimo: 5);
        await Preparar(ctx, pedido.Id, admin);

        await ctx.CrearOrderService().CambiarEstadoAsync(pedido.Id,
            new UpdateOrderStatusRequestDto { NuevoEstado = EstadosPedido.Cancelado, MotivoCancelacion = "prueba" },
            ["ADMIN"], usuarioActor: admin);

        Assert.Equal(20, ctx.Db.InventoryItems.AsNoTracking().Single(i => i.Id == insumo.Id).StockActual);
        Assert.Empty(Avisos(ctx, insumo.Id));
    }
}
