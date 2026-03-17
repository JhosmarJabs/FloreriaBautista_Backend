using Microsoft.EntityFrameworkCore;
using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Orders;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Services.Interfaces;

namespace FloreriaBautista.Services;

public class OrderService : IOrderService
{
    private readonly AppDbContext           _context;
    private readonly ILogger<OrderService>  _logger;

    // Estados válidos y sus transiciones permitidas
    private static readonly Dictionary<string, List<string>> Transiciones = new()
    {
        ["PENDIENTE_VALIDACION"] = ["VALIDADO", "CANCELADO"],
        ["VALIDADO"]             = ["EN_PROCESO", "CANCELADO"],
        ["EN_PROCESO"]           = ["LISTO", "CANCELADO"],
        ["LISTO"]                = ["EN_CAMINO", "ENTREGADO"],
        ["EN_CAMINO"]            = ["ENTREGADO"],
        ["ENTREGADO"]            = [],
        ["CANCELADO"]            = []
    };

    public OrderService(AppDbContext context, ILogger<OrderService> logger)
    {
        _context = context;
        _logger  = logger;
    }

    // ── Crear pedido cliente autenticado ──────────────────────────
    public async Task<OrderResponseDto> CrearPedidoClienteAsync(Guid userId, CreateOrderRequestDto request)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.UserId == userId)
            ?? throw new AppException("No se encontró el perfil de cliente para este usuario.");

        return await CrearPedidoAsync(customer, "WEB", request.TipoPedido, request.FechaEntrega,
            request.HoraEntrega, request.Direccion, request.Items, request.Notas);
    }

    // ── Crear pedido físico ───────────────────────────────────────
    public async Task<OrderResponseDto> CrearPedidoFisicoAsync(CreatePhysicalOrderRequestDto request)
    {
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
            request.HoraEntrega, request.Direccion, request.Items, request.Notas);
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

        return await PaginarAsync(query, page, size);
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

    // ── Admin: listar ─────────────────────────────────────────────
    public async Task<PagedResultDto<OrderSummaryDto>> ListarAdminAsync(
        string? estado, DateOnly? desde, DateOnly? hasta, int page, int size)
    {
        var query = _context.Orders
            .Include(o => o.Customer)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(estado))
            query = query.Where(o => o.EstadoPedido == estado.ToUpper());
        if (desde.HasValue)
            query = query.Where(o => o.FechaEntrega >= desde.Value);
        if (hasta.HasValue)
            query = query.Where(o => o.FechaEntrega <= hasta.Value);

        return await PaginarAsync(query.OrderByDescending(o => o.FechaCreacion), page, size);
    }

    // ── Admin: detalle ────────────────────────────────────────────
    public async Task<OrderResponseDto> ObtenerAdminAsync(Guid orderId)
        => MapToDto(await ObtenerConDetalleAsync(orderId));

    // ── Helpers ───────────────────────────────────────────────────
    private async Task<OrderResponseDto> CrearPedidoAsync(
        Customer customer, string canal, string tipo,
        DateOnly fechaEntrega, TimeOnly? horaEntrega,
        DireccionDto direccion, List<OrderItemRequestDto> items, string? notas)
    {
        decimal total = 0;
        var orderItems = new List<OrderItem>();

        foreach (var item in items)
        {
            var product = await _context.Products.FindAsync(item.ProductId)
                ?? throw new AppException($"Producto '{item.ProductId}' no encontrado.");
            if (product.Estado != "ACTIVO")
                throw new AppException($"El producto '{product.Nombre}' no está disponible.");

            var subtotal = product.PrecioBase * item.Cantidad;
            total += subtotal;
            orderItems.Add(new OrderItem
            {
                Id             = Guid.NewGuid(),
                ProductId      = product.Id,
                Cantidad       = item.Cantidad,
                PrecioUnitario = product.PrecioBase,
                Subtotal       = subtotal
            });
        }

        var order = new Order
        {
            Id                          = Guid.NewGuid(),
            CustomerId                  = customer.Id,
            TipoPedido                  = tipo.ToUpper(),
            Canal                       = canal,
            EstadoPedido                = "PENDIENTE_VALIDACION",
            FechaCreacion               = DateTime.UtcNow,
            FechaEntrega                = fechaEntrega,
            HoraEntrega                 = horaEntrega,
            Total                       = total,
            SaldoPendiente              = total,
            Notas                       = notas,
            DireccionEntregaCalle       = direccion.Calle,
            DireccionEntregaColonia     = direccion.Colonia,
            DireccionEntregaMunicipio   = direccion.Municipio,
            DireccionEntregaEstado      = direccion.Estado,
            DireccionEntregaCp          = direccion.Cp,
            DireccionEntregaReferencias = direccion.Referencias
        };

        foreach (var oi in orderItems) { oi.OrderId = order.Id; order.OrderItems.Add(oi); }

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Pedido creado: {Id} | Total: {Total}", order.Id, total);

        return MapToDto(await ObtenerConDetalleAsync(order.Id));
    }

    private async Task<Order> ObtenerConDetalleAsync(Guid id)
        => await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.Id == id)
            ?? throw new NotFoundException("Pedido", id);

    private static async Task<PagedResultDto<OrderSummaryDto>> PaginarAsync(
        IQueryable<Order> query, int page, int size)
    {
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * size).Take(size)
            .Select(o => new OrderSummaryDto
            {
                Id            = o.Id,
                EstadoPedido  = o.EstadoPedido,
                FechaEntrega  = o.FechaEntrega,
                Total         = o.Total,
                NombreCliente = o.Customer.Nombre,
                FechaCreacion = o.FechaCreacion
            }).ToListAsync();

        return new PagedResultDto<OrderSummaryDto>
        {
            Items        = items,
            Total        = total,
            Pagina       = page,
            TamanoPagina = size,
            TotalPaginas = (int)Math.Ceiling(total / (double)size)
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
        SaldoPendiente = o.SaldoPendiente,
        Notas          = o.Notas,
        NombreCliente  = o.Customer?.Nombre ?? "",
        FechaCreacion  = o.FechaCreacion,
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
        }).ToList()
    };
}
