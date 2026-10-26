using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.InstantSales;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Services;
using FloreriaBautista.Services.Audit;
using FloreriaBautista.Services.Employee;
using FloreriaBautista.Services.Interfaces;
using FloreriaBautista.Services.Realtime;
using FloreriaBautista.Services.Scheduler;
using FloreriaBautista.Services.Notifications;
using FloreriaBautista.Services.WhatsApp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
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

    // El nombre permite abrir un segundo contexto sobre la misma base (otra "petición").
    private readonly string _nombreBd = $"pedidos-{Guid.NewGuid()}";

    // Las opciones distintas dan otro proveedor interno y, con él, otra base en
    // memoria: NuevoContexto debe repetir esta configuración para ver los mismos datos.
    private readonly bool _ignorarTransacciones;

    public PedidosTestContext(DateTime ahoraUtc, params IInterceptor[] interceptores)
        : this(ahoraUtc, ignorarTransacciones: false, interceptores) { }

    /// <param name="ignorarTransacciones">
    /// La base en memoria no tiene transacciones y por defecto lanza al abrir una.
    /// Las pruebas que pasan por RegistrarPagoAsync lo activan; las demás conservan
    /// el comportamiento por defecto, del que dependen algunas (VentaDedupeTests).
    /// </param>
    public PedidosTestContext(DateTime ahoraUtc, bool ignorarTransacciones, params IInterceptor[] interceptores)
    {
        _ahoraUtc = DateTime.SpecifyKind(ahoraUtc, DateTimeKind.Utc);

        var builder = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(_nombreBd)
            .AddInterceptors(interceptores);
        _ignorarTransacciones = ignorarTransacciones;
        if (ignorarTransacciones)
            builder.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        var options = builder.Options;

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
        new(Db, Fechas, new NullRealtimeNotifier(),
            new NotificationService(Db, NullLogger<NotificationService>.Instance),
            new PricingService(Db),
            new AvailabilityService(Db, Fechas, new MemoryCache(new MemoryCacheOptions()),
                NullLogger<AvailabilityService>.Instance),
            NullLogger<OrderService>.Instance, new NullWhatsAppNotifier(), Auditoria);

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

    /// <param name="registradoPor">
    /// Quien cobró. Por defecto, quien atendió el pedido: lo mismo que la migración
    /// asigna a los pagos anteriores a que existiera la columna.
    /// </param>
    public Payment AgregarPagoEfectivo(Guid orderId, DateTime fechaPagoUtc, decimal monto,
                                       Guid? registradoPor = null)
    {
        var pago = new Payment
        {
            Id        = Guid.NewGuid(),
            OrderId   = orderId,
            Monto     = monto,
            TipoPago  = "TOTAL",
            Metodo    = "EFECTIVO",
            Estado    = "REGISTRADO",
            FechaPago = DateTime.SpecifyKind(fechaPagoUtc, DateTimeKind.Utc),
            RegistradoPorUsuarioId = registradoPor
                ?? Db.Orders.Where(o => o.Id == orderId).Select(o => o.AtendidoPorUsuarioId).FirstOrDefault()
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
    public ErrorReportService     CrearReporteService() => new(Db, Fechas, Auditoria, new NullRealtimeNotifier());

    /// <summary>Relee el pedido desde la base, sin la copia rastreada en memoria.</summary>
    public Order Recargar(Guid pedidoId)
    {
        var pedido = Db.Orders.Single(o => o.Id == pedidoId);
        Db.Entry(pedido).Reload();
        return pedido;
    }

    /// <summary>Contexto independiente sobre la misma base, sin interceptores (otra petición).</summary>
    public AppDbContext NuevoContexto()
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_nombreBd);
        if (_ignorarTransacciones)
            builder.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        return new AppDbContext(builder.Options);
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
    public Task ReporteErrorCreadoAsync(Guid reporteId, string tipoRegistro, string nombreEmpleado) => Task.CompletedTask;
    public Task PedidoCambioEstadoAsync(Guid orderId, Guid empleadoId, string nuevoEstado) => Task.CompletedTask;
}

internal sealed class NullWhatsAppNotifier : IWhatsAppNotifier
{
    public Task<int> EncolarAsync(string tipoEvento, string texto, string? entidadTipo = null,
        Guid? entidadId = null, TimeSpan? deduplicarVentana = null) => Task.FromResult(0);
    public Task<int> EncolarPruebaAsync(Guid? recipientId) => Task.FromResult(0);
}
