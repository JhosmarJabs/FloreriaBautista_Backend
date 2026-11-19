using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Models.DTOs.Orders;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FloreriaBautista.Tests;

/// <summary>
/// FIX-005 (inventario-disponibilidad-ventas-prediccion): la venta de mostrador rechazada por
/// falta de existencias deja constancia en la demanda no cubierta (origen VENTA_INSTANTANEA).
/// </summary>
public class OrderServiceDemandaMostradorTests
{
    private static readonly DateTime Ahora  = new(2026, 10, 20, 18, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Manana = new(2026, 10, 21);

    private static (PedidosTestContext Ctx, Product Producto, InventoryItem Insumo, Guid Empleado) Montar(int stock)
    {
        var ctx = new PedidosTestContext(Ahora);
        var empleado = ctx.AgregarEmpleado("Empleado");
        var insumo = new InventoryItem
        {
            Id = Guid.NewGuid(), Nombre = "Escasa", StockActual = stock,
            StockMinimo = 1, Sucursal = "MATRIZ", UnidadMedida = "pza"
        };
        ctx.Db.InventoryItems.Add(insumo);
        var producto = ctx.AgregarProducto();
        ctx.Db.ProductRecipes.Add(new ProductRecipe
        {
            Id = Guid.NewGuid(), ProductId = producto.Id,
            InventoryItemId = insumo.Id, CantidadRequerida = 10
        });
        ctx.Db.SaveChanges();
        return (ctx, producto, insumo, empleado);
    }

    private static CreatePhysicalOrderRequestDto Venta(Guid productoId, bool sinConexion = false) => new()
    {
        NombreCliente = "Cliente mostrador",
        FechaEntrega  = Manana,
        TipoPedido    = "INSTANTANEO",
        VentaSincronizadaSinConexion = sinConexion,
        Items         = [new OrderItemRequestDto { ProductId = productoId, Cantidad = 1 }]
    };

    [Fact]
    public async Task Venta_Rechazada_Por_Stock_Registra_Demanda_Con_El_Deficit()
    {
        var (ctx, producto, insumo, empleado) = Montar(stock: 5);
        using var _ = ctx;

        await Assert.ThrowsAsync<AppException>(() =>
            ctx.CrearOrderService().CrearPedidoFisicoAsync(Venta(producto.Id), empleado));

        var demanda = await ctx.Db.UncoveredDemands.AsNoTracking().SingleAsync();
        Assert.Equal(insumo.Id, demanda.InventoryItemId);
        Assert.Equal(producto.Id, demanda.ProductId);
        Assert.Equal(10, demanda.CantidadSolicitada);
        Assert.Equal(5, demanda.CantidadDisponible);
        Assert.Equal(5, demanda.Deficit);
        Assert.Equal("VENTA_INSTANTANEA", demanda.Origen);
        Assert.Equal(5, ctx.Db.InventoryItems.AsNoTracking().Single(i => i.Id == insumo.Id).StockActual);
        Assert.False(await ctx.Db.Orders.AnyAsync());
    }

    [Fact]
    public async Task Venta_Con_Stock_Suficiente_No_Registra_Demanda()
    {
        var (ctx, producto, _, empleado) = Montar(stock: 50);
        using var _ctx = ctx;

        await ctx.CrearOrderService().CrearPedidoFisicoAsync(Venta(producto.Id), empleado);

        Assert.False(await ctx.Db.UncoveredDemands.AnyAsync());
    }

    [Fact]
    public async Task Venta_Sincronizada_Sin_Conexion_Sin_Stock_Se_Acepta_Y_No_Registra_Demanda()
    {
        var (ctx, producto, _, empleado) = Montar(stock: 5);
        using var _ctx = ctx;

        await ctx.CrearOrderService().CrearPedidoFisicoAsync(Venta(producto.Id, sinConexion: true), empleado);

        Assert.False(await ctx.Db.UncoveredDemands.AnyAsync());
    }
}
