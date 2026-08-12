using FloreriaBautista.Models.Enums;
using FloreriaBautista.Tests.TestHelpers;
using Xunit;

namespace FloreriaBautista.Tests;

public class OrderServiceListarAdminTests
{
    // 11 de agosto de 2026, 23:00 en la florería (UTC-6) = 12 de agosto 05:00 UTC.
    private static readonly DateTime Utc23HoraLocal = new(2026, 8, 12, 5, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Hoy  = new(2026, 8, 11);
    private static readonly DateOnly Ayer = new(2026, 8, 10);

    [Fact]
    public async Task Vista_Activa_A_Las_23_Hora_Local_Sigue_Mostrando_Los_Pedidos_De_Hoy()
    {
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var pedido = ctx.AgregarPedido(Hoy, EstadosPedido.EnPreparacion);

        var resultado = await ctx.CrearOrderService()
            .ListarAdminAsync(null, null, null, page: 1, size: 20);

        Assert.Equal(pedido.Id, Assert.Single(resultado.Items).Id);
    }

    [Fact]
    public async Task Vista_Activa_Oculta_Los_Atrasados_Aunque_El_Archivador_No_Haya_Corrido()
    {
        // El pedido sigue con Archivado = false: es la segunda red de seguridad.
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        ctx.AgregarPedido(Ayer, EstadosPedido.EnPreparacion);

        var resultado = await ctx.CrearOrderService()
            .ListarAdminAsync(null, null, null, page: 1, size: 20);

        Assert.Empty(resultado.Items);
    }

    [Fact]
    public async Task Vista_De_Archivo_Devuelve_El_Pedido_De_Ayer()
    {
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var pedido = ctx.AgregarPedido(Ayer, EstadosPedido.EnPreparacion);
        await ctx.CrearArchivador().ArchivarAtrasadosAsync();

        var resultado = await ctx.CrearOrderService()
            .ListarAdminAsync(null, null, null, page: 1, size: 20, archivado: true);

        Assert.Equal(pedido.Id, Assert.Single(resultado.Items).Id);
    }

    [Fact]
    public async Task RequierenCierre_Devuelve_Solo_Los_Archivados_Que_Siguen_En_Ruta()
    {
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var enRuta = ctx.AgregarPedido(Ayer, EstadosPedido.EnRuta);
        ctx.AgregarPedido(Ayer, EstadosPedido.EnPreparacion);   // → NO_COMPLETADO
        ctx.AgregarPedido(Ayer, EstadosPedido.Entregado);       // ya cerrado
        ctx.AgregarPedido(Hoy,  EstadosPedido.EnRuta);          // vigente, no archivado

        await ctx.CrearArchivador().ArchivarAtrasadosAsync();

        var resultado = await ctx.CrearOrderService()
            .ListarAdminAsync(null, null, null, page: 1, size: 20, requierenCierre: true);

        Assert.Equal(enRuta.Id, Assert.Single(resultado.Items).Id);
    }

    [Fact]
    public async Task Rango_De_Fechas_Pasado_Sobre_La_Vista_Activa_Levanta_La_Bandera()
    {
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        ctx.AgregarPedido(Ayer, EstadosPedido.EnPreparacion);

        var resultado = await ctx.CrearOrderService().ListarAdminAsync(
            null, desde: new DateOnly(2026, 7, 1), hasta: new DateOnly(2026, 7, 5),
            page: 1, size: 20);

        Assert.Empty(resultado.Items);
        Assert.True(resultado.RangoFueraDeVistaActiva);
    }

    [Fact]
    public async Task Rango_De_Fechas_Futuro_No_Levanta_La_Bandera()
    {
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        ctx.AgregarPedido(Hoy.AddDays(3), EstadosPedido.PendienteValidacion);

        var resultado = await ctx.CrearOrderService().ListarAdminAsync(
            null, desde: Hoy, hasta: Hoy.AddDays(7), page: 1, size: 20);

        Assert.Single(resultado.Items);
        Assert.False(resultado.RangoFueraDeVistaActiva);
    }

    [Fact]
    public async Task El_Mismo_Rango_Pasado_Sobre_El_Archivo_No_Levanta_La_Bandera()
    {
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        ctx.AgregarPedido(Ayer, EstadosPedido.EnPreparacion);
        await ctx.CrearArchivador().ArchivarAtrasadosAsync();

        var resultado = await ctx.CrearOrderService().ListarAdminAsync(
            null, desde: Ayer, hasta: Ayer, page: 1, size: 20, archivado: true);

        Assert.Single(resultado.Items);
        Assert.False(resultado.RangoFueraDeVistaActiva);
    }
}
