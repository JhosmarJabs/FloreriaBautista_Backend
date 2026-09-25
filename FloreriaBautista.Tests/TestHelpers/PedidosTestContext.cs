using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.InstantSales;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Services;
using FloreriaBautista.Services.Audit;
using FloreriaBautista.Services.Employee;
using FloreriaBautista.Services.Interfaces;
using FloreriaBautista.Services.Realtime;
using FloreriaBautista.Services.Scheduler;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FloreriaBautista.Tests.TestHelpers;

/// <summary>
/// Andamiaje común de las pruebas de pedidos: una base en memoria aislada por
/// prueba, un cliente de ejemplo y un reloj fijo en la zona de la tienda (UTC-6)
/// para poder situarse en "las 23:00 hora local" de forma determinista.
/// </summary>
public sealed class PedidosTestContext : IDisposable
{
    /// <summary>Zona fija de la tienda; no depende de la base de zonas horarias de la máquina.</summary>
    public static readonly TimeZoneInfo ZonaTienda = TimeZoneInfo.CreateCustomTimeZone(
        "Test-UTC-6", TimeSpan.FromHours(-6), "Prueba (UTC-6)", "Prueba (UTC-6)");

    public AppDbContext Db      { get; }
    public Guid         ClienteId { get; } = Guid.NewGuid();

    private DateTime _ahoraUtc;

    public PedidosTestContext(DateTime ahoraUtc)
    {
        _ahoraUtc = DateTime.SpecifyKind(ahoraUtc, DateTimeKind.Utc);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"pedidos-{Guid.NewGuid()}")
            .Options;

        Db = new AppDbContext(options);
        Db.Customers.Add(new Customer
        {
            Id          = ClienteId,
            TipoCliente = "FISICO",
            Nombre      = "Cliente de prueba",
            Telefono    = "5550000000"
        });
        Db.SaveChanges();
    }

    /// <summary>Reloj de la prueba; puede moverse para simular el paso del tiempo.</summary>
    public void AvanzarRelojA(DateTime ahoraUtc) =>
        _ahoraUtc = DateTime.SpecifyKind(ahoraUtc, DateTimeKind.Utc);

    public IFechaHelper Fechas => new FechaHelper(ZonaTienda, () => _ahoraUtc);

    public OrderArchiver CrearArchivador() =>
        new(Db, Fechas, new AuditService(Db, NullLogger<AuditService>.Instance),
            NullLogger<OrderArchiver>.Instance);

    public OrderService CrearOrderService() =>
        new(Db, Fechas, new NullRealtimeNotifier(), NullLogger<OrderService>.Instance);

    public Order AgregarPedido(
        DateOnly fechaEntrega, string estado, bool archivado = false,
        Guid? atendidoPor = null, DateTime? fechaCreacion = null, decimal total = 500m)
    {
        var pedido = new Order
        {
            Id                   = Guid.NewGuid(),
            CustomerId           = ClienteId,
            TipoPedido           = "ANTICIPADO",
            Canal                = "WEB",
            EstadoPedido         = estado,
            FechaEntrega         = fechaEntrega,
            FechaCreacion        = fechaCreacion ?? DateTime.UtcNow.AddDays(-1),
            Total                = total,
            Archivado            = archivado,
            ArchivadoEn          = archivado ? DateTime.UtcNow.AddDays(-1) : null,
            AtendidoPorUsuarioId = atendidoPor
        };

        Db.Orders.Add(pedido);
        Db.SaveChanges();
        return pedido;
    }

    // ── Alcance del empleado ──────────────────────────────────────

    /// <summary>Crea un empleado y devuelve su id.</summary>
    public Guid AgregarEmpleado(string nombre = "Empleado")
    {
        var id = Guid.NewGuid();
        Db.Users.Add(new User
        {
            Id     = id,
            Nombre = nombre,
            Correo = $"{id:N}@prueba.local"
        });
        Db.SaveChanges();
        return id;
    }

    public Expense AgregarGasto(Guid usuarioId, DateTime fechaHoraUtc, decimal importe,
                                string concepto = "Taxi", string estado = "REGISTRADO")
    {
        var gasto = new Expense
        {
            Id        = Guid.NewGuid(),
            UsuarioId = usuarioId,
            FechaHora = DateTime.SpecifyKind(fechaHoraUtc, DateTimeKind.Utc),
            Concepto  = concepto,
            Categoria = "GASTO_PERSONAL",
            Importe   = importe,
            Estado    = estado
        };
        Db.Expenses.Add(gasto);
        Db.SaveChanges();
        return gasto;
    }

    public Payment AgregarPagoEfectivo(Guid orderId, DateTime fechaPagoUtc, decimal monto)
    {
        var pago = new Payment
        {
            Id        = Guid.NewGuid(),
            OrderId   = orderId,
            Monto     = monto,
            TipoPago  = "TOTAL",
            Metodo    = "EFECTIVO",
            Estado    = "REGISTRADO",
            FechaPago = DateTime.SpecifyKind(fechaPagoUtc, DateTimeKind.Utc)
        };
        Db.Payments.Add(pago);
        Db.SaveChanges();
        return pago;
    }

    public Product AgregarProducto(decimal precio = 350m, string nombre = "Ramo de prueba")
    {
        var producto = new Product
        {
            Id          = Guid.NewGuid(),
            Nombre      = nombre,
            Descripcion = "Producto de prueba",
            PrecioBase  = precio,
            Tipo        = "ARREGLO",
            Estado      = "ACTIVO"
        };
        Db.Products.Add(producto);
        Db.SaveChanges();
        return producto;
    }

    private IAuditService Auditoria => new AuditService(Db, NullLogger<AuditService>.Instance);

    public EmployeeExpenseService CrearGastoService() => new(Db, Fechas, Auditoria);
    public CashCutService         CrearCorteService() => new(Db, Fechas, Auditoria);
    public ErrorReportService     CrearReporteService() => new(Db, Fechas, Auditoria);

    /// <summary>Relee el pedido desde la base, sin la copia rastreada en memoria.</summary>
    public Order Recargar(Guid pedidoId)
    {
        var pedido = Db.Orders.Single(o => o.Id == pedidoId);
        Db.Entry(pedido).Reload();
        return pedido;
    }

    public void Dispose() => Db.Dispose();
}

internal sealed class NullRealtimeNotifier : IRealtimeNotifier
{
    public Task SolicitudPendienteAsync(SolicitudVentaInstantaneaDto solicitud) => Task.CompletedTask;
    public Task SolicitudEscaladaAsync(SolicitudVentaInstantaneaDto solicitud) => Task.CompletedTask;
    public Task SolicitudDecididaAsync(Guid solicitudId, SolicitudVentaInstantaneaDto solicitud) => Task.CompletedTask;
    public Task PedidoNuevoAsync(PedidoNuevoNotificacion notificacion) => Task.CompletedTask;
    public Task PedidoAnticipadoInformativoAsync(PedidoNuevoNotificacion notificacion) => Task.CompletedTask;
}
