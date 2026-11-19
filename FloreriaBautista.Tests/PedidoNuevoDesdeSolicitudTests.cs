using FloreriaBautista.Models.DTOs.Orders;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Tests.TestHelpers;
using Xunit;

namespace FloreriaBautista.Tests;

/// <summary>
/// FIX-004 (venta-instantanea-tiempo-real-05): el aviso en vivo de 60 s (PedidoNuevo) solo
/// se marca para pedidos nacidos de una solicitud de venta instantánea ACEPTADA.
/// </summary>
public class PedidoNuevoDesdeSolicitudTests
{
    private static readonly DateTime Ahora = new(2026, 10, 20, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Pedido_normal_del_cliente_no_se_marca_desde_solicitud()
    {
        using var ctx = new PedidosTestContext(Ahora, ignorarTransacciones: true);
        var userId = Guid.NewGuid();
        ctx.Db.Customers.Add(new Customer
        {
            Id = Guid.NewGuid(), UserId = userId, TipoCliente = "REGISTRADO",
            Nombre = "Ana", Apellido = "Web", Telefono = "5551112222"
        });
        var producto = ctx.AgregarProducto();
        ctx.Db.SaveChanges();
        var registro = new RegistroRealtimeNotifier();

        await ctx.CrearOrderService(registro).CrearPedidoClienteAsync(userId, new CreateOrderRequestDto
        {
            TipoPedido = "ANTICIPADO",
            FechaEntrega = new DateOnly(2026, 10, 23),
            Items = [new OrderItemRequestDto { ProductId = producto.Id, Cantidad = 1 }]
        });

        var n = Assert.Single(registro.PedidosNuevos);
        Assert.False(n.DesdeSolicitudAceptada);
    }

    [Fact]
    public async Task Venta_de_mostrador_no_se_marca_desde_solicitud()
    {
        using var ctx = new PedidosTestContext(Ahora, ignorarTransacciones: true);
        var empleado = ctx.AgregarEmpleado("Empleado");
        var producto = ctx.AgregarProducto();
        var registro = new RegistroRealtimeNotifier();

        await ctx.CrearOrderService(registro).CrearPedidoFisicoAsync(new CreatePhysicalOrderRequestDto
        {
            NombreCliente = "Cliente mostrador",
            FechaEntrega = new DateOnly(2026, 10, 21),
            TipoPedido = "INSTANTANEO",
            Items = [new OrderItemRequestDto { ProductId = producto.Id, Cantidad = 1 }]
        }, empleado);

        var n = Assert.Single(registro.PedidosNuevos);
        Assert.False(n.DesdeSolicitudAceptada);
    }

    /// <summary>
    /// FIX-005: la frontera de 7 dias usa la fecha local de la tienda. A las 20:00 locales
    /// (02:00 UTC del dia siguiente) HOY local + 7 sigue siendo informativo; + 6 no.
    /// </summary>
    [Theory]
    [InlineData(7, true)]
    [InlineData(6, false)]
    public async Task Frontera_de_7_dias_usa_la_fecha_local_de_noche(int dias, bool esInformativo)
    {
        var ahoraNoche = new DateTime(2026, 10, 21, 2, 0, 0, DateTimeKind.Utc); // 20-oct 20:00 hora local
        using var ctx = new PedidosTestContext(ahoraNoche, ignorarTransacciones: true);
        var hoyLocal = ctx.Fechas.HoyLocal();
        Assert.Equal(new DateOnly(2026, 10, 20), hoyLocal);
        var userId = Guid.NewGuid();
        ctx.Db.Customers.Add(new Customer
        {
            Id = Guid.NewGuid(), UserId = userId, TipoCliente = "REGISTRADO",
            Nombre = "Ana", Apellido = "Web", Telefono = "5551112222"
        });
        var producto = ctx.AgregarProducto();
        ctx.Db.SaveChanges();
        var registro = new RegistroRealtimeNotifier();

        await ctx.CrearOrderService(registro).CrearPedidoClienteAsync(userId, new CreateOrderRequestDto
        {
            TipoPedido = "ANTICIPADO",
            FechaEntrega = hoyLocal.AddDays(dias),
            Items = [new OrderItemRequestDto { ProductId = producto.Id, Cantidad = 1 }]
        });

        Assert.Equal(esInformativo ? 1 : 0, registro.Informativos.Count);
        Assert.Equal(esInformativo ? 0 : 1, registro.PedidosNuevos.Count);
    }

    // El caso positivo (pedido desde solicitud aceptada) no se puede ejercitar con EF InMemory:
    // el vinculo solicitud-pedido usa ExecuteUpdate. Lo cubre el retest RT-004-B contra la app real.
}
