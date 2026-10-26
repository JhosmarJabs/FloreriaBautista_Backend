using System.Text.Json;
using FloreriaBautista.Models.DTOs.Orders;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Enums;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FloreriaBautista.Tests;

/// <summary>
/// Cancelar un pedido exige motivo (se guarda en su propio campo, sin tocar las
/// notas) y cada cambio de estado y cada pago quedan en audit_logs. Los nombres
/// de Detalles en minúscula los lee PeopleReportsService.
/// </summary>
public class OrderServiceAuditoriaTests
{
    private static readonly DateTime Ahora  = new(2026, 10, 20, 18, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Manana = new(2026, 10, 21);

    private static UpdateOrderStatusRequestDto Estado(string nuevo, string? motivo = null) =>
        new() { NuevoEstado = nuevo, MotivoCancelacion = motivo };

    private static List<AuditLog> Bitacora(PedidosTestContext ctx, Guid orderId, string accion) =>
        ctx.Db.AuditLogs.AsNoTracking()
            .Where(l => l.Entidad == "Order" && l.EntidadId == orderId.ToString() && l.Accion == accion)
            .ToList();

    private static JsonElement Detalles(AuditLog log) =>
        JsonDocument.Parse(log.Detalles!).RootElement;

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Cancelar_Sin_Motivo_Se_Rechaza_Sin_Cambiar_Nada(string? motivo)
    {
        using var ctx = new PedidosTestContext(Ahora);
        var admin  = ctx.AgregarEmpleado("Admin");
        var pedido = ctx.AgregarPedido(Manana, EstadosPedido.PendienteValidacion);

        await Assert.ThrowsAsync<AppException>(() =>
            ctx.CrearOrderService().CambiarEstadoAsync(pedido.Id,
                Estado(EstadosPedido.Cancelado, motivo), ["ADMIN"], usuarioActor: admin));

        var guardado = ctx.Recargar(pedido.Id);
        Assert.Equal(EstadosPedido.PendienteValidacion, guardado.EstadoPedido);
        Assert.Null(guardado.MotivoCancelacion);
        Assert.Empty(ctx.Db.AuditLogs.AsNoTracking().ToList());
    }

    [Fact]
    public async Task Cancelar_Con_Motivo_Mayor_A_500_Caracteres_Se_Rechaza()
    {
        using var ctx = new PedidosTestContext(Ahora);
        var pedido = ctx.AgregarPedido(Manana, EstadosPedido.PendienteValidacion);

        await Assert.ThrowsAsync<AppException>(() =>
            ctx.CrearOrderService().CambiarEstadoAsync(pedido.Id,
                Estado(EstadosPedido.Cancelado, new string('x', 501)), ["ADMIN"]));

        Assert.Equal(EstadosPedido.PendienteValidacion, ctx.Recargar(pedido.Id).EstadoPedido);
    }

    [Fact]
    public async Task Cancelar_Con_Motivo_Lo_Guarda_Sin_Tocar_Las_Notas_Y_Lo_Audita()
    {
        using var ctx = new PedidosTestContext(Ahora);
        var admin  = ctx.AgregarEmpleado("Admin");
        var pedido = ctx.AgregarPedido(Manana, EstadosPedido.PendienteValidacion);
        pedido.Notas = "Tocar el timbre dos veces";
        ctx.Db.SaveChanges();

        var resultado = await ctx.CrearOrderService().CambiarEstadoAsync(pedido.Id,
            Estado(EstadosPedido.Cancelado, "  Cliente ya no lo quiere  "), ["ADMIN"], usuarioActor: admin);

        Assert.Equal(EstadosPedido.Cancelado, resultado.EstadoPedido);
        Assert.Equal("Cliente ya no lo quiere", resultado.MotivoCancelacion);

        var guardado = ctx.Recargar(pedido.Id);
        Assert.Equal("Cliente ya no lo quiere", guardado.MotivoCancelacion);
        Assert.Equal("Tocar el timbre dos veces", guardado.Notas);

        var log = Assert.Single(Bitacora(ctx, pedido.Id, "CAMBIO_ESTADO"));
        Assert.Equal(admin, log.UsuarioId);
        var detalles = Detalles(log);
        Assert.Equal(EstadosPedido.PendienteValidacion, detalles.GetProperty("anterior").GetString());
        Assert.Equal(EstadosPedido.Cancelado, detalles.GetProperty("nuevo").GetString());
        Assert.Equal("Cliente ya no lo quiere", detalles.GetProperty("motivo").GetString());
    }

    [Fact]
    public async Task Validar_Audita_El_Cambio_Con_Quien_Actua_E_Ignora_El_Motivo()
    {
        using var ctx = new PedidosTestContext(Ahora);
        var empleado = ctx.AgregarEmpleado();
        var pedido   = ctx.AgregarPedido(Manana, EstadosPedido.PendienteValidacion);

        await ctx.CrearOrderService().CambiarEstadoAsync(pedido.Id,
            Estado(EstadosPedido.EnPreparacion, "no aplica"), ["ADMIN"], usuarioActor: empleado);

        Assert.Null(ctx.Recargar(pedido.Id).MotivoCancelacion);

        var log = Assert.Single(Bitacora(ctx, pedido.Id, "CAMBIO_ESTADO"));
        Assert.Equal(empleado, log.UsuarioId);
        var detalles = Detalles(log);
        Assert.Equal(EstadosPedido.PendienteValidacion, detalles.GetProperty("anterior").GetString());
        Assert.Equal(EstadosPedido.EnPreparacion, detalles.GetProperty("nuevo").GetString());
        Assert.Equal(JsonValueKind.Null, detalles.GetProperty("motivo").ValueKind);
    }

    [Fact]
    public async Task Registrar_Pago_Audita_Monto_Metodo_Y_Saldos()
    {
        using var ctx = new PedidosTestContext(Ahora, ignorarTransacciones: true);
        var admin  = ctx.AgregarEmpleado("Admin");
        var pedido = ctx.AgregarPedido(Manana, EstadosPedido.EnPreparacion);
        pedido.SaldoPendiente = 500m;
        ctx.Db.SaveChanges();

        await ctx.CrearOrderService().RegistrarPagoAsync(pedido.Id,
            new RegisterPaymentRequestDto { Monto = 200m, Metodo = "efectivo" }, usuarioActor: admin);

        var log = Assert.Single(Bitacora(ctx, pedido.Id, "PAGO_REGISTRADO"));
        Assert.Equal(admin, log.UsuarioId);
        var detalles = Detalles(log);
        Assert.Equal(200m, detalles.GetProperty("monto").GetDecimal());
        Assert.Equal("EFECTIVO", detalles.GetProperty("metodo").GetString());
        Assert.Equal("ANTICIPO", detalles.GetProperty("tipoPago").GetString());
        Assert.Equal(500m, detalles.GetProperty("saldoAnterior").GetDecimal());
        Assert.Equal(300m, detalles.GetProperty("saldoNuevo").GetDecimal());
    }
}
