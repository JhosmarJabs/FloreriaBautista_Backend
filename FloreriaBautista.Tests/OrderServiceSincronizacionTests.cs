using FloreriaBautista.Models.DTOs.Orders;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Enums;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FloreriaBautista.Tests;

/// <summary>
/// Escrituras de pedidos que mueven inventario: deben sellar ActualizadoEn del
/// insumo (si no, el cambio nunca llega al delta de inventario), y la devolución
/// por cancelación debe caber en la columna motivo_categoria (varchar(20)).
/// </summary>
public class OrderServiceSincronizacionTests
{
    // 11 de agosto de 2026, 23:00 en la florería (UTC-6) = 12 de agosto 05:00 UTC.
    private static readonly DateTime Utc23HoraLocal = new(2026, 8, 12, 5, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Manana = new(2026, 8, 12);
    private static readonly DateTime MarcaAnterior = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

    private static (Order Pedido, InventoryItem Insumo, Guid Admin) PrepararPedidoConReceta(PedidosTestContext ctx)
    {
        var admin    = ctx.AgregarEmpleado("Admin");
        var producto = ctx.AgregarProducto();
        var insumo   = new InventoryItem
        {
            Id            = Guid.NewGuid(),
            Nombre        = "Rosa roja",
            Sucursal      = "PRINCIPAL",
            StockActual   = 100,
            ActualizadoEn = MarcaAnterior
        };
        ctx.Db.InventoryItems.Add(insumo);
        ctx.Db.ProductRecipes.Add(new ProductRecipe
        {
            Id = Guid.NewGuid(), ProductId = producto.Id, InventoryItemId = insumo.Id, CantidadRequerida = 3
        });

        var pedido = ctx.AgregarPedido(Manana, EstadosPedido.PendienteValidacion);
        ctx.Db.OrderItems.Add(new OrderItem
        {
            Id = Guid.NewGuid(), OrderId = pedido.Id, ProductId = producto.Id,
            Cantidad = 2, PrecioUnitario = 350m, Subtotal = 700m
        });
        ctx.Db.SaveChanges();
        return (pedido, insumo, admin);
    }

    [Fact]
    public async Task Pasar_A_Preparacion_Sella_El_Insumo_Descontado()
    {
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var (pedido, insumo, admin) = PrepararPedidoConReceta(ctx);

        await ctx.CrearOrderService().CambiarEstadoAsync(pedido.Id,
            new UpdateOrderStatusRequestDto { NuevoEstado = EstadosPedido.EnPreparacion },
            ["ADMIN"], usuarioActor: admin);

        var guardado = await ctx.Db.InventoryItems.AsNoTracking().SingleAsync(i => i.Id == insumo.Id);
        Assert.Equal(94, guardado.StockActual);
        Assert.Equal(Utc23HoraLocal, guardado.ActualizadoEn);
        Assert.Equal(Utc23HoraLocal, ctx.Recargar(pedido.Id).ActualizadoEn);
    }

    [Fact]
    public async Task Cancelar_En_Preparacion_Devuelve_El_Stock_Con_Categoria_Que_Cabe_En_La_Columna()
    {
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var (pedido, insumo, admin) = PrepararPedidoConReceta(ctx);
        var service = ctx.CrearOrderService();

        await service.CambiarEstadoAsync(pedido.Id,
            new UpdateOrderStatusRequestDto { NuevoEstado = EstadosPedido.EnPreparacion },
            ["ADMIN"], usuarioActor: admin);
        var resultado = await service.CambiarEstadoAsync(pedido.Id,
            new UpdateOrderStatusRequestDto
            {
                NuevoEstado       = EstadosPedido.Cancelado,
                MotivoCancelacion = "Cliente ya no lo quiere" // cancelar exige motivo
            },
            ["ADMIN"], usuarioActor: admin);

        Assert.Equal(EstadosPedido.Cancelado, resultado.EstadoPedido);

        var guardado = await ctx.Db.InventoryItems.AsNoTracking().SingleAsync(i => i.Id == insumo.Id);
        Assert.Equal(100, guardado.StockActual);
        Assert.Equal(Utc23HoraLocal, guardado.ActualizadoEn);

        var devolucion = await ctx.Db.InventoryMovements.SingleAsync(m => m.TipoMovimiento == "ENTRADA");
        Assert.Equal("DEVOLUCION", devolucion.MotivoCategoria);
        Assert.True(devolucion.MotivoCategoria.Length <= 20); // inventory_movements.motivo_categoria varchar(20)
    }

    [Fact]
    public async Task Pedido_Fisico_Con_Tipo_No_Admitido_Se_Rechaza_Sin_Crear_El_Cliente()
    {
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var producto = ctx.AgregarProducto();

        await Assert.ThrowsAsync<AppException>(() => ctx.CrearOrderService().CrearPedidoFisicoAsync(
            new CreatePhysicalOrderRequestDto
            {
                NombreCliente = "QA cliente tipo invalido",
                FechaEntrega  = Manana,
                TipoPedido    = "PROGRAMADO",
                Items         = [new OrderItemRequestDto { ProductId = producto.Id, Cantidad = 1 }]
            }));

        Assert.False(await ctx.Db.Customers.AnyAsync(c => c.Nombre == "QA cliente tipo invalido"));
    }
}
