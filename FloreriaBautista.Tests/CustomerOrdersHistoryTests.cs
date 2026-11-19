using FloreriaBautista.Controllers;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Orders;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Enums;
using FloreriaBautista.Tests.TestHelpers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FloreriaBautista.Tests;

/// <summary>
/// Historial de pedidos de un cliente: sumaTotal (todos los pedidos menos los
/// cancelados, independiente de la página) y paginación acotada
/// (page ≥ 1, 1 ≤ size ≤ 100). InMemory no reproduce el error de OFFSET negativo
/// de PostgreSQL: se comprueban los valores devueltos.
/// </summary>
public class CustomerOrdersHistoryTests
{
    private static readonly DateTime Ahora = new(2026, 10, 21, 18, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Entrega = new(2026, 10, 25);

    private static async Task<PagedResultDto<OrderSummaryDto>> Pedir(
        PedidosTestContext ctx, Guid clienteId, int page = 1, int size = 20)
    {
        var resp = await new CustomersController(ctx.Db).PedidosDeCliente(clienteId, page, size);
        var ok = Assert.IsType<OkObjectResult>(resp);
        var envoltura = Assert.IsType<ApiResponseDto<PagedResultDto<OrderSummaryDto>>>(ok.Value);
        return envoltura.Data!;
    }

    private static void SembrarMixto(PedidosTestContext ctx)
    {
        ctx.AgregarPedido(Entrega, EstadosPedido.Entregado, total: 100m);
        ctx.AgregarPedido(Entrega, EstadosPedido.Cancelado, total: 1000m);
        ctx.AgregarPedido(Entrega, "CANCELADA", total: 2000m);
        ctx.AgregarPedido(Entrega, "PENDIENTE_ANULACION", total: 30m);
        ctx.AgregarPedido(Entrega, "NO_COMPLETADO", total: 4m);
    }

    [Fact]
    public async Task SumaTotal_excluye_cancelados_y_total_los_cuenta()
    {
        using var ctx = new PedidosTestContext(Ahora);
        SembrarMixto(ctx);

        var r = await Pedir(ctx, ctx.ClienteId);

        Assert.Equal(134m, r.SumaTotal);
        Assert.Equal(5, r.Total);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(2, 2)]
    [InlineData(100, 1)]
    [InlineData(1, 9)]
    public async Task SumaTotal_no_depende_de_page_ni_size(int size, int page)
    {
        using var ctx = new PedidosTestContext(Ahora);
        SembrarMixto(ctx);

        var r = await Pedir(ctx, ctx.ClienteId, page, size);

        Assert.Equal(134m, r.SumaTotal);
        Assert.Equal(5, r.Total);
    }

    [Fact]
    public async Task SumaTotal_cubre_mas_de_100_pedidos()
    {
        using var ctx = new PedidosTestContext(Ahora);
        for (var i = 0; i < 120; i++)
            ctx.AgregarPedido(Entrega, EstadosPedido.Entregado, total: 10m);

        var r = await Pedir(ctx, ctx.ClienteId, 1, 100);

        Assert.Equal(1200m, r.SumaTotal);
        Assert.Equal(120, r.Total);
        Assert.Equal(100, r.Items.Count);
        Assert.Equal(2, r.TotalPaginas);
    }

    [Fact]
    public async Task Cliente_sin_pedidos_devuelve_cero()
    {
        using var ctx = new PedidosTestContext(Ahora);

        var r = await Pedir(ctx, ctx.ClienteId);

        Assert.Equal(0m, r.SumaTotal);
        Assert.Equal(0, r.Total);
        Assert.Equal(0, r.TotalPaginas);
    }

    [Fact]
    public async Task Cliente_solo_con_cancelados_devuelve_cero_pero_total_mayor_que_cero()
    {
        using var ctx = new PedidosTestContext(Ahora);
        ctx.AgregarPedido(Entrega, EstadosPedido.Cancelado, total: 500m);
        ctx.AgregarPedido(Entrega, "CANCELADA", total: 700m);

        var r = await Pedir(ctx, ctx.ClienteId);

        Assert.Equal(0m, r.SumaTotal);
        Assert.Equal(2, r.Total);
    }

    [Fact]
    public async Task Cliente_inexistente_devuelve_404()
    {
        using var ctx = new PedidosTestContext(Ahora);

        var resp = await new CustomersController(ctx.Db).PedidosDeCliente(Guid.NewGuid(), 1, 20);

        Assert.IsType<NotFoundObjectResult>(resp);
    }

    [Fact]
    public async Task Pedido_de_otro_cliente_no_entra_en_la_suma()
    {
        using var ctx = new PedidosTestContext(Ahora);
        ctx.AgregarPedido(Entrega, EstadosPedido.Entregado, total: 100m);
        var otro = Guid.NewGuid();
        ctx.Db.Customers.Add(new Customer
        {
            Id = otro, TipoCliente = "FISICO", Nombre = "Otro", Telefono = "5551111111"
        });
        ctx.Db.Orders.Add(new Order
        {
            Id = Guid.NewGuid(), CustomerId = otro, TipoPedido = "ANTICIPADO", Canal = "WEB",
            EstadoPedido = EstadosPedido.Entregado, FechaEntrega = Entrega,
            FechaCreacion = Ahora.AddDays(-1), Total = 9999m
        });
        ctx.Db.SaveChanges();

        var r = await Pedir(ctx, ctx.ClienteId);

        Assert.Equal(100m, r.SumaTotal);
        Assert.Equal(1, r.Total);
    }

    [Theory]
    [InlineData(500, 100)]
    [InlineData(101, 100)]
    [InlineData(100, 100)]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(int.MaxValue, 100)]
    public async Task Size_se_acota_entre_1_y_100(int pedido, int esperado)
    {
        using var ctx = new PedidosTestContext(Ahora);
        for (var i = 0; i < 105; i++)
            ctx.AgregarPedido(Entrega, EstadosPedido.Entregado, total: 1m);

        var r = await Pedir(ctx, ctx.ClienteId, 1, pedido);

        Assert.Equal(esperado, r.TamanoPagina);
        Assert.Equal(esperado, r.Items.Count);
        Assert.Equal((int)Math.Ceiling(105 / (double)esperado), r.TotalPaginas);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Page_cero_o_negativa_se_corrige_a_1(int page)
    {
        using var ctx = new PedidosTestContext(Ahora);
        SembrarMixto(ctx);

        var r = await Pedir(ctx, ctx.ClienteId, page, 20);

        Assert.Equal(1, r.Pagina);
        Assert.Equal(5, r.Items.Count);
    }

    [Fact]
    public async Task Valores_validos_conservan_orden_y_conteos()
    {
        using var ctx = new PedidosTestContext(Ahora);
        var viejo = ctx.AgregarPedido(Entrega, EstadosPedido.Entregado, fechaCreacion: Ahora.AddDays(-3), total: 1m);
        var nuevo = ctx.AgregarPedido(Entrega, "CONFIRMADO", fechaCreacion: Ahora.AddDays(-1), total: 2m);

        var r = await Pedir(ctx, ctx.ClienteId, 1, 100);

        Assert.Equal(new[] { nuevo.Id, viejo.Id }, r.Items.Select(i => i.Id).ToArray());
        Assert.Equal(2, r.Total);
        Assert.Equal(1, r.TotalPaginas);
        Assert.Equal(1, r.Pagina);
        Assert.Equal(100, r.TamanoPagina);
    }
}
