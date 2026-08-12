using System.Text.Json;
using FloreriaBautista.Models.Enums;
using FloreriaBautista.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FloreriaBautista.Tests;

public class OrderArchiverTests
{
    // 11 de agosto de 2026, 23:00 en la florería (UTC-6) = 12 de agosto 05:00 UTC.
    private static readonly DateTime Utc23HoraLocal = new(2026, 8, 12, 5, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Hoy  = new(2026, 8, 11);
    private static readonly DateOnly Ayer = new(2026, 8, 10);

    [Fact]
    public async Task Pedido_Con_Entrega_Hoy_No_Se_Archiva_A_Las_23_Hora_Local()
    {
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var pedido = ctx.AgregarPedido(Hoy, EstadosPedido.EnPreparacion);

        var resultado = await ctx.CrearArchivador().ArchivarAtrasadosAsync();

        Assert.Equal(0, resultado.Total);
        Assert.Equal(Hoy, resultado.Fecha);

        var guardado = ctx.Recargar(pedido.Id);
        Assert.False(guardado.Archivado);
        Assert.Equal(EstadosPedido.EnPreparacion, guardado.EstadoPedido);
    }

    [Fact]
    public async Task Pedido_Con_Entrega_De_Ayer_Se_Archiva()
    {
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var pedido = ctx.AgregarPedido(Ayer, EstadosPedido.EnPreparacion);

        var resultado = await ctx.CrearArchivador().ArchivarAtrasadosAsync();

        Assert.Equal(1, resultado.Total);

        var guardado = ctx.Recargar(pedido.Id);
        Assert.True(guardado.Archivado);
        Assert.NotNull(guardado.ArchivadoEn);
    }

    [Theory]
    [InlineData(EstadosPedido.PendienteValidacion)]
    [InlineData(EstadosPedido.EnPreparacion)]
    [InlineData(EstadosPedido.PendienteAnulacion)]
    public async Task Estados_Sin_Seguimiento_Pasan_A_No_Completado(string estado)
    {
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var pedido = ctx.AgregarPedido(Ayer, estado);

        var resultado = await ctx.CrearArchivador().ArchivarAtrasadosAsync();

        Assert.Equal(1, resultado.NoCompletados);
        Assert.Equal(EstadosPedido.NoCompletado, ctx.Recargar(pedido.Id).EstadoPedido);
    }

    [Fact]
    public async Task Pedido_En_Ruta_Atrasado_Conserva_Su_Estado()
    {
        // Un repartidor puede seguir entregando pasadas las 23:00: el pedido sí
        // ocurrió, solo nadie lo cerró. Marcarlo NO_COMPLETADO sería falsear el dato.
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var pedido = ctx.AgregarPedido(Ayer, EstadosPedido.EnRuta);

        var resultado = await ctx.CrearArchivador().ArchivarAtrasadosAsync();

        Assert.Equal(1, resultado.RequierenCierre);
        Assert.Equal(0, resultado.NoCompletados);

        var guardado = ctx.Recargar(pedido.Id);
        Assert.True(guardado.Archivado);
        Assert.Equal(EstadosPedido.EnRuta, guardado.EstadoPedido);
    }

    [Theory]
    [InlineData(EstadosPedido.Entregado)]
    [InlineData(EstadosPedido.Cancelado)]
    public async Task Pedido_Ya_Cerrado_Solo_Se_Mueve_Al_Archivo(string estado)
    {
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var pedido = ctx.AgregarPedido(Ayer, estado);

        var resultado = await ctx.CrearArchivador().ArchivarAtrasadosAsync();

        Assert.Equal(1, resultado.YaCerrados);
        Assert.Equal(estado, ctx.Recargar(pedido.Id).EstadoPedido);
        Assert.True(ctx.Recargar(pedido.Id).Archivado);
    }

    [Fact]
    public async Task La_Pasada_Es_Idempotente()
    {
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var pedido = ctx.AgregarPedido(Ayer, EstadosPedido.PendienteValidacion);

        var primera = await ctx.CrearArchivador().ArchivarAtrasadosAsync();
        var archivadoEn = ctx.Recargar(pedido.Id).ArchivadoEn;

        var segunda = await ctx.CrearArchivador().ArchivarAtrasadosAsync();

        Assert.Equal(1, primera.Total);
        Assert.Equal(0, segunda.Total);
        Assert.Equal(0, segunda.NoCompletados);

        var guardado = ctx.Recargar(pedido.Id);
        Assert.Equal(archivadoEn, guardado.ArchivadoEn);      // no se resella la fecha
        Assert.Equal(EstadosPedido.NoCompletado, guardado.EstadoPedido);
        Assert.Single(await ctx.Db.AuditLogs.ToListAsync());  // ni se duplica la auditoría
    }

    [Fact]
    public async Task Cada_Transicion_Queda_Registrada_En_Auditoria()
    {
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var pedido = ctx.AgregarPedido(Ayer, EstadosPedido.EnPreparacion);

        await ctx.CrearArchivador().ArchivarAtrasadosAsync();

        var log = Assert.Single(await ctx.Db.AuditLogs.ToListAsync());
        Assert.Equal("ARCHIVADO_AUTOMATICO", log.Accion);
        Assert.Equal("Order", log.Entidad);
        Assert.Equal(pedido.Id.ToString(), log.EntidadId);
        Assert.Null(log.UsuarioId);   // lo disparó el scheduler, no una persona

        var detalles = JsonDocument.Parse(log.Detalles!).RootElement;
        Assert.Equal(EstadosPedido.EnPreparacion, detalles.GetProperty("EstadoAnterior").GetString());
        Assert.Equal(EstadosPedido.NoCompletado,  detalles.GetProperty("EstadoNuevo").GetString());
        Assert.Equal("AUTOMATICO",                detalles.GetProperty("Disparo").GetString());
    }

    [Fact]
    public async Task La_Pasada_Manual_Registra_Al_Admin_Que_La_Disparo()
    {
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        ctx.AgregarPedido(Ayer, EstadosPedido.EnPreparacion);
        var adminId = Guid.NewGuid();

        await ctx.CrearArchivador().ArchivarAtrasadosAsync(adminId);

        var log = Assert.Single(await ctx.Db.AuditLogs.ToListAsync());
        Assert.Equal(adminId, log.UsuarioId);
        Assert.Equal("MANUAL", JsonDocument.Parse(log.Detalles!).RootElement.GetProperty("Disparo").GetString());
    }

    [Fact]
    public async Task Al_Cruzar_La_Medianoche_Local_El_Pedido_De_Ese_Dia_Ya_Se_Archiva()
    {
        // 12 de agosto 00:30 local = 06:30 UTC: ahora el pedido del día 11 sí venció.
        using var ctx = new PedidosTestContext(Utc23HoraLocal);
        var pedido = ctx.AgregarPedido(Hoy, EstadosPedido.EnPreparacion);

        Assert.Equal(0, (await ctx.CrearArchivador().ArchivarAtrasadosAsync()).Total);

        ctx.AvanzarRelojA(new DateTime(2026, 8, 12, 6, 30, 0, DateTimeKind.Utc));
        var resultado = await ctx.CrearArchivador().ArchivarAtrasadosAsync();

        Assert.Equal(1, resultado.Total);
        Assert.True(ctx.Recargar(pedido.Id).Archivado);
    }
}
