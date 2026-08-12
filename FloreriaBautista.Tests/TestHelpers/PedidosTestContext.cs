using FloreriaBautista.Data;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Services;
using FloreriaBautista.Services.Audit;
using FloreriaBautista.Services.Interfaces;
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
        new(Db, Fechas, NullLogger<OrderService>.Instance);

    public Order AgregarPedido(DateOnly fechaEntrega, string estado, bool archivado = false)
    {
        var pedido = new Order
        {
            Id            = Guid.NewGuid(),
            CustomerId    = ClienteId,
            TipoPedido    = "ANTICIPADO",
            Canal         = "WEB",
            EstadoPedido  = estado,
            FechaEntrega  = fechaEntrega,
            FechaCreacion = DateTime.UtcNow.AddDays(-1),
            Total         = 500m,
            Archivado     = archivado,
            ArchivadoEn   = archivado ? DateTime.UtcNow.AddDays(-1) : null
        };

        Db.Orders.Add(pedido);
        Db.SaveChanges();
        return pedido;
    }

    /// <summary>Relee el pedido desde la base, sin la copia rastreada en memoria.</summary>
    public Order Recargar(Guid pedidoId)
    {
        var pedido = Db.Orders.Single(o => o.Id == pedidoId);
        Db.Entry(pedido).Reload();
        return pedido;
    }

    public void Dispose() => Db.Dispose();
}
