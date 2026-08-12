using Microsoft.EntityFrameworkCore;
using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Orders;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Enums;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Services.Interfaces;

namespace FloreriaBautista.Services;

public class OrderService : IOrderService
{
    private readonly AppDbContext           _context;
    private readonly IFechaHelper           _fechas;
    private readonly ILogger<OrderService>  _logger;

    // Estados válidos y sus transiciones permitidas
    private static readonly Dictionary<string, List<string>> Transiciones = new()
    {
        ["PENDIENTE_VALIDACION"] = ["EN_PREPARACION", "CANCELADO", "PENDIENTE_ANULACION"],
        ["EN_PREPARACION"]       = ["EN_RUTA", "CANCELADO", "PENDIENTE_ANULACION"],
        ["EN_RUTA"]              = ["ENTREGADO", "PENDIENTE_ANULACION"],
        ["ENTREGADO"]            = [],
        ["CANCELADO"]            = [],
        ["PENDIENTE_ANULACION"]  = ["CANCELADO", "EN_PREPARACION"]
    };

    public OrderService(AppDbContext context, IFechaHelper fechas, ILogger<OrderService> logger)
    {
        _context = context;
        _fechas  = fechas;
        _logger  = logger;
    }

    // ── Crear pedido cliente autenticado ──────────────────────────
    public async Task<OrderResponseDto> CrearPedidoClienteAsync(Guid userId, CreateOrderRequestDto request)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.UserId == userId)
            ?? throw new AppException("No se encontró el perfil de cliente para este usuario.");

        return await CrearPedidoAsync(customer, "WEB", request.TipoPedido, request.FechaEntrega,
            request.HoraEntrega, request.Direccion, request.Items, request.Notas,
            costoEnvio: request.CostoEnvio);
    }

    // ── Crear pedido físico ───────────────────────────────────────
    public async Task<OrderResponseDto> CrearPedidoFisicoAsync(CreatePhysicalOrderRequestDto request)
    {
        var esInstantaneo = request.TipoPedido.Trim().ToUpper() == "INSTANTANEO";

        // La dirección solo es obligatoria si el pedido sí implica una entrega a domicilio.
        if (!esInstantaneo && request.Direccion == null)
            throw new AppException("La dirección de entrega es obligatoria para pedidos anticipados.");

        // Buscar o crear cliente físico
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.TipoCliente == "FISICO" &&
                c.Nombre == request.NombreCliente.Trim());

        if (customer == null)
        {
            customer = new Customer
            {
                Id          = Guid.NewGuid(),
                TipoCliente = "FISICO",
                Nombre      = request.NombreCliente.Trim(),
                Telefono    = request.Telefono ?? "",
                CreadoEn    = DateTime.UtcNow
            };
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();
        }

        return await CrearPedidoAsync(customer, "FISICO", request.TipoPedido, request.FechaEntrega,
            request.HoraEntrega, request.Direccion, request.Items, request.Notas,
            entregaInmediata: esInstantaneo,
            montoPagado: request.MontoPagado, metodoPago: request.MetodoPago,
            costoEnvio: request.CostoEnvio);
    }

    // ── Mis pedidos ───────────────────────────────────────────────
    public async Task<PagedResultDto<OrderSummaryDto>> ListarMisPedidosAsync(Guid userId, int page, int size)
    {
        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
        if (customer == null) return new PagedResultDto<OrderSummaryDto>();

        var query = _context.Orders
            .Include(o => o.Customer)
            .Where(o => o.CustomerId == customer.Id)
            .OrderByDescending(o => o.FechaCreacion);

        var resultado = await PaginarAsync(query, page, size);

        // Adjuntar los productos de cada pedido para mostrarlos en "Mis Pedidos".
        var ids = resultado.Items.Select(i => i.Id).ToList();
        var itemsPorOrden = await _context.OrderItems
            .Where(oi => ids.Contains(oi.OrderId))
            .Select(oi => new
            {
                oi.OrderId,
                Dto = new OrderSummaryItemDto
                {
                    ProductName  = oi.Product.Nombre,
                    ProductImage = oi.Product.ImagenUrl,
                    Quantity     = oi.Cantidad,
                    Price        = oi.PrecioUnitario
                }
            })
            .ToListAsync();

        var porOrden = itemsPorOrden
            .GroupBy(x => x.OrderId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Dto).ToList());

        foreach (var o in resultado.Items)
            o.Items = porOrden.TryGetValue(o.Id, out var lista) ? lista : new();

        return resultado;
    }

    // ── Mi pedido por ID ──────────────────────────────────────────
    public async Task<OrderResponseDto> ObtenerMiPedidoAsync(Guid userId, Guid orderId)
    {
        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId)
            ?? throw new NotFoundException("Cliente", userId);

        var order = await ObtenerConDetalleAsync(orderId);
        if (order.CustomerId != customer.Id)
            throw new UnauthorizedException("No tienes acceso a este pedido.");

        return MapToDto(order);
    }

    // ── Cambiar estado ────────────────────────────────────────────
    public async Task<OrderResponseDto> CambiarEstadoAsync(
        Guid orderId, UpdateOrderStatusRequestDto request, List<string> rolesUsuario)
    {
        var order = await ObtenerConDetalleAsync(orderId);
        var nuevoEstado = request.NuevoEstado.ToUpper();

        if (!Transiciones.TryGetValue(order.EstadoPedido, out var permitidos) ||
            !permitidos.Contains(nuevoEstado))
            throw new AppException(
                $"No se puede cambiar de '{order.EstadoPedido}' a '{nuevoEstado}'.");

        order.EstadoPedido = nuevoEstado;
        if (!string.IsNullOrWhiteSpace(request.Notas))
            order.Notas = request.Notas;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Pedido {Id}: {Anterior} → {Nuevo}", orderId, order.EstadoPedido, nuevoEstado);
        return MapToDto(order);
    }

    // ── Registrar pago (anticipo / liquidación posterior) ──────────
    public async Task<OrderResponseDto> RegistrarPagoAsync(Guid orderId, RegisterPaymentRequestDto request)
    {
        var order = await ObtenerConDetalleAsync(orderId);

        if (order.EstadoPedido == "CANCELADO")
            throw new AppException("No se pueden registrar pagos sobre un pedido cancelado.");

        if (request.Monto > order.SaldoPendiente)
            throw new AppException(
                $"El monto (${request.Monto}) excede el saldo pendiente (${order.SaldoPendiente}).");

        var nuevoSaldo = order.SaldoPendiente - request.Monto;

        var payment = new Payment
        {
            Id        = Guid.NewGuid(),
            OrderId   = order.Id,
            Monto     = request.Monto,
            TipoPago  = nuevoSaldo == 0 ? "LIQUIDACION" : "ANTICIPO",
            Metodo    = request.Metodo.Trim().ToUpper(),
            FechaPago = DateTime.UtcNow,
            Estado    = "REGISTRADO"
        };

        await using var tx = await _context.Database.BeginTransactionAsync();

        // Se guardan por separado (INSERT de payments, luego UPDATE de orders)
        // dentro de la misma transacción: mezclar ambas operaciones en un solo
        // SaveChanges producía un DbUpdateConcurrencyException espurio.
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        order.SaldoPendiente = nuevoSaldo;
        await _context.SaveChangesAsync();

        await tx.CommitAsync();

        _logger.LogInformation("Pago registrado: Pedido {Id} | Monto: {Monto} | Saldo restante: {Saldo}",
            orderId, request.Monto, nuevoSaldo);

        return MapToDto(order);
    }

    // ── Admin: listar ─────────────────────────────────────────────
    public async Task<PagedResultDto<OrderSummaryDto>> ListarAdminAsync(
        string? estado, DateOnly? desde, DateOnly? hasta, int page, int size,
        bool archivado = false, bool requierenCierre = false)
    {
        // "Hoy" en hora de la tienda, nunca en UTC: la florería está en UTC-6 y a
        // partir de las 18:00 locales el día UTC ya es el siguiente, lo que hacía
        // desaparecer de la vista los pedidos que se entregan hoy mismo.
        var hoy = _fechas.HoyLocal();

        var query = _context.Orders
            .Include(o => o.Customer)
            .AsQueryable();

        if (requierenCierre)
        {
            // Vista "requieren cierre": pedidos ya archivados que conservaron un
            // estado en curso (EN_RUTA) porque su fecha de entrega pasó mientras el
            // repartidor seguía en la calle. Sí ocurrieron, pero nadie los cerró.
            query = query.Where(o => o.Archivado && !EstadosPedido.Cerrados.Contains(o.EstadoPedido));
        }
        else
        {
            query = query.Where(o => o.Archivado == archivado);

            // RED DE SEGURIDAD DOBLE — no borrar este filtro pensando que sobra:
            // la vista activa solo muestra pedidos de hoy en adelante. Los atrasados
            // los mueve OrderArchiver, pero solo revisa cada hora; sin este filtro un
            // pedido vencido seguiría visible hasta 60 min. Ver OrderArchiverService.
            if (!archivado)
                query = query.Where(o => o.FechaEntrega >= hoy);
        }

        if (!string.IsNullOrWhiteSpace(estado))
            query = query.Where(o => o.EstadoPedido == estado.ToUpper());
        if (desde.HasValue)
            query = query.Where(o => o.FechaEntrega >= desde.Value);
        if (hasta.HasValue)
            query = query.Where(o => o.FechaEntrega <= hasta.Value);

        var resultado = await PaginarAsync(query.OrderByDescending(o => o.FechaCreacion), page, size);

        // Un rango de fechas que cae (aunque sea en parte) antes de hoy choca con el
        // filtro duro de la vista activa: el usuario pide "del 1 al 5 del mes pasado"
        // y no sale nada. En vez de reinterpretar la consulta en silencio, se avisa
        // con esta bandera para que el frontend ofrezca "ver en el archivo".
        resultado.RangoFueraDeVistaActiva = !archivado && !requierenCierre &&
            ((desde.HasValue && desde.Value < hoy) || (hasta.HasValue && hasta.Value < hoy));

        return resultado;
    }

    // ── Admin: detalle ────────────────────────────────────────────
    public async Task<OrderResponseDto> ObtenerAdminAsync(Guid orderId)
        => MapToDto(await ObtenerConDetalleAsync(orderId));

    // ── Helpers ───────────────────────────────────────────────────
    private async Task<OrderResponseDto> CrearPedidoAsync(
        Customer customer, string canal, string tipo,
        DateOnly fechaEntrega, TimeOnly? horaEntrega,
        DireccionDto? direccion, List<OrderItemRequestDto> items, string? notas,
        bool entregaInmediata = false,
        decimal? montoPagado = null, string? metodoPago = null, decimal? costoEnvio = null)
    {
        decimal total = costoEnvio ?? 0;
        var orderItems = new List<OrderItem>();

        foreach (var item in items)
        {
            var product = await _context.Products.FindAsync(item.ProductId)
                ?? throw new AppException($"Producto '{item.ProductId}' no encontrado.");
            if (product.Estado != "ACTIVO")
                throw new AppException($"El producto '{product.Nombre}' no está disponible.");

            total += product.PrecioBase * item.Cantidad;
            orderItems.Add(new OrderItem
            {
                Id             = Guid.NewGuid(),
                ProductId      = product.Id,
                Cantidad       = item.Cantidad,
                PrecioUnitario = product.PrecioBase
            });
        }

        // Cuánto se cobra al crear el pedido: si no se especifica un monto,
        // una venta instantánea de mostrador se asume pagada en su totalidad
        // (comportamiento previo); un pedido anticipado sin monto queda sin abono.
        // Si se asume pagada sin que nos digan el método, no se registra un
        // Payment (no inventamos un método), solo se salda SaldoPendiente.
        decimal pagado;
        var metodoUsado = metodoPago;
        if (montoPagado.HasValue)
        {
            if (montoPagado.Value > 0 && string.IsNullOrWhiteSpace(metodoPago))
                throw new AppException("Debe indicar el método de pago para registrar el cobro.");
            pagado = Math.Min(montoPagado.Value, total);
        }
        else
        {
            pagado = entregaInmediata ? total : 0;
            metodoUsado = null;
        }

        var order = new Order
        {
            Id                          = Guid.NewGuid(),
            CustomerId                  = customer.Id,
            TipoPedido                  = tipo.ToUpper(),
            Canal                       = canal,
            // Una venta instantánea de mostrador se paga y se entrega ahí mismo:
            // no pasa por preparación ni ruta de entrega.
            EstadoPedido                = entregaInmediata ? "ENTREGADO" : "PENDIENTE_VALIDACION",
            FechaCreacion               = DateTime.UtcNow,
            FechaEntrega                = fechaEntrega,
            HoraEntrega                 = horaEntrega,
            Total                       = total,
            CostoEnvio                  = costoEnvio,
            SaldoPendiente              = total - pagado,
            Notas                       = notas,
            DireccionEntregaCalle       = direccion?.Calle ?? "",
            DireccionEntregaColonia     = direccion?.Colonia ?? "",
            DireccionEntregaMunicipio   = direccion?.Municipio ?? "",
            DireccionEntregaEstado      = direccion?.Estado ?? "",
            DireccionEntregaCp          = direccion?.Cp,
            DireccionEntregaReferencias = direccion?.Referencias
        };

        foreach (var oi in orderItems) { oi.OrderId = order.Id; order.OrderItems.Add(oi); }

        if (pagado > 0 && !string.IsNullOrWhiteSpace(metodoUsado))
        {
            order.Payments.Add(new Payment
            {
                Id        = Guid.NewGuid(),
                OrderId   = order.Id,
                Monto     = pagado,
                TipoPago  = pagado >= total ? "TOTAL" : "ANTICIPO",
                Metodo    = metodoUsado.Trim().ToUpper(),
                FechaPago = DateTime.UtcNow,
                Estado    = "REGISTRADO"
            });
        }

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Pedido creado: {Id} | Total: {Total} | Pagado: {Pagado}", order.Id, total, pagado);

        return MapToDto(await ObtenerConDetalleAsync(order.Id));
    }

    private async Task<Order> ObtenerConDetalleAsync(Guid id)
        => await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == id)
            ?? throw new NotFoundException("Pedido", id);

    private static async Task<PagedResultDto<OrderSummaryDto>> PaginarAsync(
        IQueryable<Order> query, int page, int size)
    {
        if (page <= 0) page = 1;
        if (size <= 0) size = 10;
        if (size > 100) size = 100;

        var total = await query.CountAsync();
        // Recaudación bruta: suma de TODOS los pedidos filtrados, no solo la página.
        var sumaTotal = total == 0 ? 0m : await query.SumAsync(o => o.Total);
        var items = await query.Skip((page - 1) * size).Take(size)
            .Select(o => new OrderSummaryDto
            {
                Id            = o.Id,
                EstadoPedido  = o.EstadoPedido,
                FechaEntrega  = o.FechaEntrega,
                Total         = o.Total,
                NombreCliente = o.Customer.Nombre,
                FechaCreacion = o.FechaCreacion,
                Archivado     = o.Archivado
            }).ToListAsync();

        return new PagedResultDto<OrderSummaryDto>
        {
            Items        = items,
            Total        = total,
            Pagina       = page,
            TamanoPagina = size,
            TotalPaginas = (int)Math.Ceiling(total / (double)size),
            SumaTotal    = sumaTotal
        };
    }

    private static OrderResponseDto MapToDto(Order o) => new()
    {
        Id             = o.Id,
        EstadoPedido   = o.EstadoPedido,
        TipoPedido     = o.TipoPedido,
        Canal          = o.Canal,
        FechaEntrega   = o.FechaEntrega,
        HoraEntrega    = o.HoraEntrega,
        Total          = o.Total,
        CostoEnvio     = o.CostoEnvio,
        SaldoPendiente = o.SaldoPendiente,
        Notas          = o.Notas,
        NombreCliente  = o.Customer?.Nombre ?? "",
        FechaCreacion  = o.FechaCreacion,
        Archivado      = o.Archivado,
        Direccion = new DireccionDto
        {
            Calle       = o.DireccionEntregaCalle,
            Colonia     = o.DireccionEntregaColonia,
            Municipio   = o.DireccionEntregaMunicipio,
            Estado      = o.DireccionEntregaEstado,
            Cp          = o.DireccionEntregaCp,
            Referencias = o.DireccionEntregaReferencias
        },
        Items = o.OrderItems.Select(oi => new OrderItemResponseDto
        {
            Id             = oi.Id,
            ProductId      = oi.ProductId,
            NombreProducto = oi.Product?.Nombre ?? "",
            Cantidad       = oi.Cantidad,
            PrecioUnitario = oi.PrecioUnitario,
            Subtotal       = oi.Subtotal
        }).ToList(),
        Pagos = (o.Payments ?? []).OrderBy(p => p.FechaPago).Select(p => new PaymentResponseDto
        {
            Id        = p.Id,
            Monto     = p.Monto,
            TipoPago  = p.TipoPago,
            Metodo    = p.Metodo,
            FechaPago = p.FechaPago,
            Estado    = p.Estado
        }).ToList()
    };
}
