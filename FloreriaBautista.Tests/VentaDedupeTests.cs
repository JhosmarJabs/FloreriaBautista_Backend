using FloreriaBautista.Models.DTOs.Orders;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Xunit;

namespace FloreriaBautista.Tests;

/// <summary>
/// Idempotencia de la venta de mostrador por IdLocalOffline: un reenvío nunca
/// crea un segundo pedido, ni secuencial ni simultáneo, y solo le devuelve la
/// venta a quien la registró.
///
/// InMemory no aplica índices únicos, así que la carrera real (dos peticiones
/// que pasan la consulta previa) se simula con un interceptor: justo antes del
/// SaveChanges del pedido, "otra petición" guarda la misma venta y el guardado
/// falla como lo haría PostgreSQL (23505 sobre ix_orders_id_local_offline).
/// La carrera contra PostgreSQL de verdad se verifica en QA.
/// </summary>
public class VentaDedupeTests
{
    // 11 de agosto de 2026, 12:00 en la florería (UTC-6).
    private static readonly DateTime Mediodia = new(2026, 8, 11, 18, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Hoy      = new(2026, 8, 11);

    private const string IndiceIdLocalOffline = "ix_orders_id_local_offline";

    private static CreatePhysicalOrderRequestDto Venta(Guid productoId, Guid idLocalOffline) => new()
    {
        NombreCliente  = "Cliente mostrador",
        FechaEntrega   = Hoy,
        TipoPedido     = "INSTANTANEO",
        Items          = [new OrderItemRequestDto { ProductId = productoId, Cantidad = 1 }],
        MontoPagado    = 350m,
        MetodoPago     = "EFECTIVO",
        IdLocalOffline = idLocalOffline
    };

    // ── Reenvío secuencial ────────────────────────────────────────

    [Fact]
    public async Task El_Reenvio_Del_Mismo_Empleado_Devuelve_La_Misma_Venta()
    {
        using var ctx = new PedidosTestContext(Mediodia);
        var ana      = ctx.AgregarEmpleado("Ana");
        var producto = ctx.AgregarProducto(precio: 350m);
        var x        = Guid.NewGuid();

        var primera = await ctx.CrearOrderService().CrearPedidoFisicoAsync(Venta(producto.Id, x), ana);
        var reenvio = await ctx.CrearOrderService().CrearPedidoFisicoAsync(Venta(producto.Id, x), ana);

        Assert.Equal(primera.Id, reenvio.Id);
        Assert.Single(ctx.Db.Orders.Where(o => o.IdLocalOffline == x));
    }

    [Fact]
    public async Task El_Reenvio_De_Otro_Empleado_Responde_Conflicto_Sin_Crear_Pedido()
    {
        using var ctx = new PedidosTestContext(Mediodia);
        var ana      = ctx.AgregarEmpleado("Ana");
        var beto     = ctx.AgregarEmpleado("Beto");
        var producto = ctx.AgregarProducto(precio: 350m);
        var x        = Guid.NewGuid();

        await ctx.CrearOrderService().CrearPedidoFisicoAsync(Venta(producto.Id, x), ana);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            ctx.CrearOrderService().CrearPedidoFisicoAsync(Venta(producto.Id, x), beto));

        Assert.DoesNotContain(x.ToString(), ex.Message);
        Assert.Single(ctx.Db.Orders.Where(o => o.IdLocalOffline == x));
    }

    [Fact]
    public async Task Un_Pedido_Viejo_Sin_Atribucion_Se_Devuelve_Como_Siempre()
    {
        using var ctx = new PedidosTestContext(Mediodia);
        var ana      = ctx.AgregarEmpleado("Ana");
        var producto = ctx.AgregarProducto(precio: 350m);
        var x        = Guid.NewGuid();

        var viejo = ctx.AgregarPedido(Hoy, "ENTREGADO", atendidoPor: null);
        viejo.IdLocalOffline = x;
        ctx.Db.SaveChanges();

        var reenvio = await ctx.CrearOrderService().CrearPedidoFisicoAsync(Venta(producto.Id, x), ana);

        Assert.Equal(viejo.Id, reenvio.Id);
    }

    // ── Reenvío simultáneo (colisión con el índice único) ─────────

    [Fact]
    public async Task La_Colision_Simultanea_Devuelve_La_Venta_Ya_Guardada_Y_Descuenta_Stock_Una_Vez()
    {
        var x         = Guid.NewGuid();
        var colision  = new ColisionAlGuardarPedido(Error23505(IndiceIdLocalOffline));
        using var ctx = new PedidosTestContext(Mediodia, colision);

        var ana      = ctx.AgregarEmpleado("Ana");
        var producto = ctx.AgregarProducto(precio: 350m);
        var insumo   = AgregarInsumoConReceta(ctx, producto.Id, stock: 10, porProducto: 2);

        // La "otra petición" de Ana gana la carrera: guarda el pedido y descuenta el stock.
        var ganador = Guid.NewGuid();
        colision.OtraPeticion = () => GuardarVentaGanadora(ctx, ganador, x, ana, insumo, descuento: 2);

        var respuesta = await ctx.CrearOrderService().CrearPedidoFisicoAsync(Venta(producto.Id, x), ana);

        Assert.True(colision.Disparada);
        Assert.Equal(ganador, respuesta.Id);

        using var bd = ctx.NuevoContexto();
        Assert.Single(bd.Orders.Where(o => o.IdLocalOffline == x));
        Assert.Equal(8, bd.InventoryItems.Single(i => i.Id == insumo).StockActual);
        Assert.Empty(bd.InventoryMovements.Where(m => m.InventoryItemId == insumo));
        Assert.Single(bd.Payments.Where(p => p.OrderId == ganador));
        Assert.Empty(ctx.Db.ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged));
    }

    [Fact]
    public async Task La_Colision_Simultanea_Con_Venta_De_Otro_Empleado_Responde_Conflicto()
    {
        var x         = Guid.NewGuid();
        var colision  = new ColisionAlGuardarPedido(Error23505(IndiceIdLocalOffline));
        using var ctx = new PedidosTestContext(Mediodia, colision);

        var ana      = ctx.AgregarEmpleado("Ana");
        var beto     = ctx.AgregarEmpleado("Beto");
        var producto = ctx.AgregarProducto(precio: 350m);

        colision.OtraPeticion = () => GuardarVentaGanadora(ctx, Guid.NewGuid(), x, ana);

        await Assert.ThrowsAsync<ConflictException>(() =>
            ctx.CrearOrderService().CrearPedidoFisicoAsync(Venta(producto.Id, x), beto));

        using var bd = ctx.NuevoContexto();
        Assert.Single(bd.Orders.Where(o => o.IdLocalOffline == x));
    }

    public static TheoryData<string, string?> OtrosErrores => new()
    {
        { PostgresErrorCodes.UniqueViolation,     "ix_orders_otra_restriccion" },
        { PostgresErrorCodes.ForeignKeyViolation, IndiceIdLocalOffline },
        { PostgresErrorCodes.UniqueViolation,     null }
    };

    [Theory]
    [MemberData(nameof(OtrosErrores))]
    public async Task Otro_Error_Al_Guardar_Se_Propaga_Igual_Que_Antes(string sqlState, string? restriccion)
    {
        var x         = Guid.NewGuid();
        var colision  = new ColisionAlGuardarPedido(
            new PostgresException("simulado", "ERROR", "ERROR", sqlState, constraintName: restriccion));
        using var ctx = new PedidosTestContext(Mediodia, colision);

        var ana      = ctx.AgregarEmpleado("Ana");
        var producto = ctx.AgregarProducto(precio: 350m);
        colision.OtraPeticion = () => GuardarVentaGanadora(ctx, Guid.NewGuid(), x, ana);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            ctx.CrearOrderService().CrearPedidoFisicoAsync(Venta(producto.Id, x), ana));
    }

    [Fact]
    public async Task Una_Excepcion_Sin_Causa_De_Postgres_Se_Propaga()
    {
        var x         = Guid.NewGuid();
        var colision  = new ColisionAlGuardarPedido(innerException: null);
        using var ctx = new PedidosTestContext(Mediodia, colision);

        var ana      = ctx.AgregarEmpleado("Ana");
        var producto = ctx.AgregarProducto(precio: 350m);
        colision.OtraPeticion = () => GuardarVentaGanadora(ctx, Guid.NewGuid(), x, ana);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            ctx.CrearOrderService().CrearPedidoFisicoAsync(Venta(producto.Id, x), ana));
    }

    // ── Andamiaje ─────────────────────────────────────────────────

    private static PostgresException Error23505(string restriccion) =>
        new("duplicate key value violates unique constraint", "ERROR", "ERROR",
            PostgresErrorCodes.UniqueViolation, constraintName: restriccion);

    private static Guid AgregarInsumoConReceta(PedidosTestContext ctx, Guid productoId, int stock, int porProducto)
    {
        var insumo = new InventoryItem { Id = Guid.NewGuid(), Nombre = "Rosa roja", StockActual = stock };
        ctx.Db.InventoryItems.Add(insumo);
        ctx.Db.ProductRecipes.Add(new ProductRecipe
        {
            Id                = Guid.NewGuid(),
            ProductId         = productoId,
            InventoryItemId   = insumo.Id,
            CantidadRequerida = porProducto
        });
        ctx.Db.SaveChanges();
        return insumo.Id;
    }

    /// <summary>Lo que guardó la petición que ganó la carrera, desde su propio contexto.</summary>
    private static async Task GuardarVentaGanadora(PedidosTestContext ctx, Guid pedidoId, Guid idLocalOffline,
        Guid atendidoPor, Guid? insumo = null, int descuento = 0)
    {
        using var otra = ctx.NuevoContexto();
        otra.Orders.Add(new Order
        {
            Id                   = pedidoId,
            IdLocalOffline       = idLocalOffline,
            CustomerId           = ctx.ClienteId,
            TipoPedido           = "INSTANTANEO",
            Canal                = "FISICO",
            EstadoPedido         = "ENTREGADO",
            FechaCreacion        = Mediodia,
            FechaEntrega         = Hoy,
            Total                = 350m,
            AtendidoPorUsuarioId = atendidoPor
        });
        otra.Payments.Add(new Payment
        {
            Id        = Guid.NewGuid(),
            OrderId   = pedidoId,
            Monto     = 350m,
            TipoPago  = "TOTAL",
            Metodo    = "EFECTIVO",
            Estado    = "REGISTRADO",
            FechaPago = Mediodia
        });
        if (insumo.HasValue)
            otra.InventoryItems.Single(i => i.Id == insumo.Value).StockActual -= descuento;
        await otra.SaveChangesAsync();
    }

    /// <summary>
    /// Al primer guardado que incluya un pedido nuevo con IdLocalOffline, ejecuta
    /// la "otra petición" y hace fallar el guardado como lo haría PostgreSQL.
    /// </summary>
    private sealed class ColisionAlGuardarPedido(PostgresException? innerException) : SaveChangesInterceptor
    {
        public Func<Task> OtraPeticion { get; set; } = () => Task.CompletedTask;
        public bool       Disparada    { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var guardaVentaOffline = eventData.Context!.ChangeTracker.Entries<Order>()
                .Any(e => e.State == EntityState.Added && e.Entity.IdLocalOffline.HasValue);

            if (Disparada || !guardaVentaOffline)
                return result;

            Disparada = true;
            await OtraPeticion();
            throw new DbUpdateException("Guardado fallido simulado.", innerException);
        }
    }
}
