using FloreriaBautista.Models.DTOs.Orders;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Services;
using FloreriaBautista.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FloreriaBautista.Tests;

/// <summary>
/// FIX-003 (inventario-disponibilidad-ventas-prediccion): la validación del pedido anticipado suma el
/// consumo de todas las líneas por insumo (ADM-VANT-010).
/// </summary>
public class AvailabilityAnticipadaLineasTests
{
    // Martes 2026-10-20 12:00 en la tienda (UTC-6); el próximo abastecimiento es el lunes 26.
    private static readonly DateTime Ahora = new(2026, 10, 20, 18, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Hoy   = new(2026, 10, 20);

    /// <summary>10 rosas; FLOR usa 1 rosa por pieza y RAMO2 usa 2.</summary>
    private static (PedidosTestContext Ctx, InventoryItem Rosa, Product Flor, Product Ramo) Montar(int stock = 10)
    {
        var ctx = new PedidosTestContext(Ahora);
        var rosa = new InventoryItem
        {
            Id = Guid.NewGuid(), Nombre = "Rosa roja", StockActual = stock,
            StockMinimo = 1, Sucursal = "MATRIZ", UnidadMedida = "pza"
        };
        ctx.Db.InventoryItems.Add(rosa);
        var flor = ctx.AgregarProducto(nombre: "FLOR");
        var ramo = ctx.AgregarProducto(nombre: "RAMO2");
        ctx.Db.ProductRecipes.Add(new ProductRecipe { Id = Guid.NewGuid(), ProductId = flor.Id, InventoryItemId = rosa.Id, CantidadRequerida = 1 });
        ctx.Db.ProductRecipes.Add(new ProductRecipe { Id = Guid.NewGuid(), ProductId = ramo.Id, InventoryItemId = rosa.Id, CantidadRequerida = 2 });
        ctx.Db.SaveChanges();
        return (ctx, rosa, flor, ramo);
    }

    private static AvailabilityService Servicio(PedidosTestContext ctx) =>
        new(ctx.Db, ctx.Fechas, new MemoryCache(new MemoryCacheOptions()), NullLogger<AvailabilityService>.Instance);

    private static CreatePhysicalOrderRequestDto Pedido(DateOnly fecha, params (Guid Producto, int Cantidad)[] lineas) => new()
    {
        NombreCliente = "Cliente anticipado",
        FechaEntrega  = fecha,
        TipoPedido    = "ANTICIPADO",
        Items         = lineas.Select(l => new OrderItemRequestDto { ProductId = l.Producto, Cantidad = l.Cantidad }).ToList(),
        Direccion     = new DireccionDto { Calle = "Av. Juárez 120", Colonia = "Centro", Municipio = "Huejutla", Estado = "Hidalgo" }
    };

    [Fact]
    public async Task Lineas_que_juntas_superan_el_insumo_no_se_aceptan()
    {
        var (ctx, rosa, flor, ramo) = Montar();
        using var _ = ctx;

        // 6 rosas + 6 rosas = 12 contra 10; por separado cada una cabe.
        var r = await Servicio(ctx).ValidarVentaAnticipadaAsync([(flor.Id, 6), (ramo.Id, 3)], Hoy);

        Assert.False(r.PuedeAceptarse);
        var ins = Assert.Single(r.InsumosPorAbastecer);
        Assert.Equal(rosa.Id, ins.InventoryItemId);
        Assert.Equal(2, ins.CantidadFaltante);
        Assert.Equal("SIN_TIEMPO", ins.Estado);
    }

    [Fact]
    public async Task Lineas_que_juntas_caben_se_aceptan()
    {
        var (ctx, _, flor, ramo) = Montar();
        using var _ = ctx;

        // 5 + 4 rosas = 9 contra 10.
        var r = await Servicio(ctx).ValidarVentaAnticipadaAsync([(flor.Id, 5), (ramo.Id, 2)], Hoy);

        Assert.True(r.PuedeAceptarse);
        Assert.Equal("Disponibilidad suficiente con stock actual.", r.Mensaje);
    }

    [Fact]
    public async Task Producto_inexistente_sigue_dando_404()
    {
        var (ctx, _, flor, _) = Montar();
        using var _ = ctx;

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Servicio(ctx).ValidarVentaAnticipadaAsync([(flor.Id, 1), (Guid.NewGuid(), 1)], Hoy));
    }

    [Fact]
    public async Task Una_linea_se_valida_igual_que_antes()
    {
        var (ctx, _, flor, _) = Montar();
        using var _ = ctx;
        var svc = Servicio(ctx);

        Assert.True((await svc.ValidarVentaAnticipadaAsync(flor.Id, 10, Hoy)).PuedeAceptarse);
        var r = await svc.ValidarVentaAnticipadaAsync(flor.Id, 11, Hoy);
        Assert.False(r.PuedeAceptarse);
        Assert.Equal(1, Assert.Single(r.InsumosPorAbastecer).CantidadFaltante);
    }

    [Fact]
    public async Task Pedido_de_dos_lineas_que_exceden_se_rechaza_y_registra_la_demanda()
    {
        var (ctx, rosa, flor, ramo) = Montar();
        using var _ = ctx;
        var ana = ctx.AgregarEmpleado("Ana");

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            ctx.CrearOrderService().CrearPedidoFisicoAsync(Pedido(Hoy, (flor.Id, 6), (ramo.Id, 3)), ana));

        Assert.Contains("Rosa roja", ex.Message);
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(ex.Message, "Fecha sugerida").Count);
        Assert.DoesNotContain(flor.Id.ToString(), ex.Message);
        Assert.Empty(ctx.Db.Orders.AsNoTracking().ToList());
        var demanda = Assert.Single(ctx.Db.UncoveredDemands.AsNoTracking().ToList());
        Assert.Equal(rosa.Id, demanda.InventoryItemId);
        Assert.Equal(2, demanda.Deficit);
        Assert.Equal("VENTA_ANTICIPADA", demanda.Origen);
        Assert.Null(demanda.ProductId);
    }

    [Fact]
    public async Task Pedido_de_dos_lineas_que_caben_se_crea()
    {
        var (ctx, _, flor, ramo) = Montar();
        using var _ = ctx;
        var ana = ctx.AgregarEmpleado("Ana");

        var pedido = await ctx.CrearOrderService().CrearPedidoFisicoAsync(Pedido(Hoy, (flor.Id, 5), (ramo.Id, 2)), ana);

        Assert.Equal("PENDIENTE_VALIDACION", pedido.EstadoPedido);
        Assert.Empty(ctx.Db.UncoveredDemands.AsNoTracking().ToList());
    }
}
