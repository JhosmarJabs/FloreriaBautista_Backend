using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Enums;
using FloreriaBautista.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using FloreriaBautista.Services;
using Xunit;

namespace FloreriaBautista.Tests;

/// <summary>
/// FIX-002 (inventario-disponibilidad-ventas-prediccion): el comprometido solo cuenta lo que
/// aún no salió del stock. Un pedido normal ya descontó al entrar en EN_PREPARACION.
/// </summary>
public class AvailabilityComprometidoTests
{
    private static readonly DateTime Ahora  = new(2026, 10, 20, 18, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Manana = new(2026, 10, 21);

    private static (PedidosTestContext Ctx, InventoryItem Insumo, Product Producto) Montar()
    {
        var ctx = new PedidosTestContext(Ahora);
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
        ctx.Db.SaveChanges();
        return (ctx, insumo, producto);
    }

    private static Order AgregarPedido(PedidosTestContext ctx, Product p, string estado,
        bool instantaneo = false, decimal saldo = 0m)
    {
        var pedido = ctx.AgregarPedido(Manana, estado);
        pedido.SaldoPendiente = saldo;
        ctx.Db.OrderItems.Add(new OrderItem
        {
            Id = Guid.NewGuid(), OrderId = pedido.Id, ProductId = p.Id,
            Cantidad = 1, PrecioUnitario = 350m, Subtotal = 350m
        });
        if (instantaneo)
        {
            ctx.Db.SolicitudesVentaInstantanea.Add(new SolicitudVentaInstantanea
            {
                Id = Guid.NewGuid(), CustomerId = ctx.ClienteId, ProductId = p.Id,
                Cantidad = 1, Estado = "ACEPTADA", OrderId = pedido.Id
            });
        }
        ctx.Db.SaveChanges();
        return pedido;
    }

    private static async Task<int> Comprometido(PedidosTestContext ctx, Guid insumoId)
    {
        var svc = new AvailabilityService(ctx.Db, ctx.Fechas,
            new MemoryCache(new MemoryCacheOptions()), NullLogger<AvailabilityService>.Instance);
        return (await svc.ObtenerDisponibilidadInsumoAsync(insumoId)).ComprometidoPendientes;
    }

    [Fact]
    public async Task Pedido_normal_pendiente_compromete()
    {
        var (ctx, insumo, p) = Montar();
        using var _ = ctx;
        AgregarPedido(ctx, p, EstadosPedido.PendienteValidacion);
        Assert.Equal(10, await Comprometido(ctx, insumo.Id));
    }

    [Theory]
    [InlineData("EN_PREPARACION")]
    [InlineData("EN_RUTA")]
    public async Task Pedido_normal_ya_descontado_no_compromete(string estado)
    {
        var (ctx, insumo, p) = Montar();
        using var _ = ctx;
        AgregarPedido(ctx, p, estado);
        Assert.Equal(0, await Comprometido(ctx, insumo.Id));
    }

    [Theory]
    [InlineData("PENDIENTE_VALIDACION")]
    [InlineData("EN_PREPARACION")]
    [InlineData("EN_RUTA")]
    public async Task Venta_instantanea_con_saldo_compromete(string estado)
    {
        var (ctx, insumo, p) = Montar();
        using var _ = ctx;
        AgregarPedido(ctx, p, estado, instantaneo: true, saldo: 350m);
        Assert.Equal(10, await Comprometido(ctx, insumo.Id));
    }

    [Fact]
    public async Task Venta_instantanea_pagada_no_compromete()
    {
        var (ctx, insumo, p) = Montar();
        using var _ = ctx;
        AgregarPedido(ctx, p, EstadosPedido.PendienteValidacion, instantaneo: true, saldo: 0m);
        Assert.Equal(0, await Comprometido(ctx, insumo.Id));
    }

    [Fact]
    public async Task Pedido_cancelado_no_compromete()
    {
        var (ctx, insumo, p) = Montar();
        using var _ = ctx;
        AgregarPedido(ctx, p, EstadosPedido.Cancelado);
        Assert.Equal(0, await Comprometido(ctx, insumo.Id));
    }
}
