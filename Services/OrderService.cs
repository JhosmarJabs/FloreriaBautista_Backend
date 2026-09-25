using Microsoft.EntityFrameworkCore;
using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Orders;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Enums;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Services.Employee;
using FloreriaBautista.Services.Interfaces;
using FloreriaBautista.Services.Notifications;
using FloreriaBautista.Services.Realtime;

namespace FloreriaBautista.Services;

public class OrderService : IOrderService
{
    private readonly AppDbContext            _context;
    private readonly IFechaHelper            _fechas;
    private readonly IRealtimeNotifier       _realtime;
    private readonly INotificationService    _notificaciones;
    private readonly IPricingService         _pricing;
    private readonly ILogger<OrderService>   _logger;

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

    public OrderService(AppDbContext context, IFechaHelper fechas, IRealtimeNotifier realtime,
        INotificationService notificaciones, IPricingService pricing, ILogger<OrderService> logger)
    {
        _context        = context;
        _fechas         = fechas;
        _realtime       = realtime;
        _notificaciones = notificaciones;
        _pricing        = pricing;
        _logger         = logger;
    }

    // ── Crear pedido cliente autenticado ──────────────────────────
    public async Task<OrderResponseDto> CrearPedidoClienteAsync(Guid userId, CreateOrderRequestDto request)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.UserId == userId)
            ?? throw new AppException("No se encontró el perfil de cliente para este usuario.");

        return await CrearPedidoAsync(customer, "WEB", request.TipoPedido, request.FechaEntrega,
            request.HoraEntrega, request.Direccion, request.Items, request.Notas,
            costoEnvio: request.CostoEnvio, codigoCupon: request.CodigoCupon);
    }

    // ── Crear pedido físico ───────────────────────────────────────
    public async Task<OrderResponseDto> CrearPedidoFisicoAsync(
        CreatePhysicalOrderRequestDto request, Guid? atendidoPorUsuarioId = null)
    {
        // Deduplicación offline-first: si ya existe un pedido con este idLocalOffline,
        // devolvemos el existente en vez de crear uno nuevo.
        if (request.IdLocalOffline.HasValue)
        {
            var existente = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.IdLocalOffline == request.IdLocalOffline);
            if (existente != null)
                return MapToDto(existente);
        }

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
                CreadoEn    = _fechas.AhoraUtc()
            };
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();
        }

        return await CrearPedidoAsync(customer, "FISICO", request.TipoPedido, request.FechaEntrega,
            request.HoraEntrega, request.Direccion, request.Items, request.Notas,
            entregaInmediata: esInstantaneo,
            atendidoPorUsuarioId: atendidoPorUsuarioId,
            montoPagado: request.MontoPagado, metodoPago: request.MetodoPago,
            costoEnvio: request.CostoEnvio,
            idLocalOffline: request.IdLocalOffline);
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
    /// <param name="restringirAEmpleado">
    /// Cuando viene un id, el pedido debe estar dentro del alcance de ese
    /// empleado o la llamada falla. Null = sin restricción (admin o proceso del
    /// sistema). Sin esto, cualquier empleado podría mover por id el pedido de
    /// cualquier otro con solo adivinar el Guid.
    /// </param>
    public async Task<OrderResponseDto> CambiarEstadoAsync(
        Guid orderId, UpdateOrderStatusRequestDto request, List<string> rolesUsuario,
        Guid? restringirAEmpleado = null)
    {
        await AsegurarVisibleAsync(orderId, restringirAEmpleado);

        var order = await ObtenerConDetalleAsync(orderId);
        var nuevoEstado    = request.NuevoEstado.ToUpper();
        var estadoAnterior = order.EstadoPedido;

        if (!Transiciones.TryGetValue(estadoAnterior, out var permitidos) ||
            !permitidos.Contains(nuevoEstado))
            throw new AppException(
                $"No se puede cambiar de '{estadoAnterior}' a '{nuevoEstado}'.");

        order.EstadoPedido = nuevoEstado;
        if (!string.IsNullOrWhiteSpace(request.Notas))
            order.Notas = request.Notas;

        // Un pedido web o telefónico llega sin dueño; quien lo saca de
        // PENDIENTE_VALIDACION es quien lo atendió. Solo se escribe si estaba
        // vacío: el primero que lo tocó se queda con la atribución.
        if (order.AtendidoPorUsuarioId is null && restringirAEmpleado.HasValue)
            order.AtendidoPorUsuarioId = restringirAEmpleado;

        order.ActualizadoEn = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Pedido {Id}: {Anterior} → {Nuevo}", orderId, estadoAnterior, nuevoEstado);

        if (nuevoEstado == "ENTREGADO" && order.CustomerId != Guid.Empty)
        {
            try
            {
                var nombreCliente = order.Customer?.Nombre ?? "Cliente";
                await _notificaciones.CrearParaClienteAsync(
                    order.CustomerId,
                    "PEDIDO_ENTREGADO",
                    "Tu pedido fue entregado",
                    $"Hola {nombreCliente}, tu pedido ha sido entregado exitosamente.",
                    "Order", order.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al notificar PEDIDO_ENTREGADO para pedido {Id}", orderId);
            }
        }

        return MapToDto(order);
    }

    // ── Registrar pago (anticipo / liquidación posterior) ──────────
    public async Task<OrderResponseDto> RegistrarPagoAsync(
        Guid orderId, RegisterPaymentRequestDto request, Guid? restringirAEmpleado = null)
    {
        await AsegurarVisibleAsync(orderId, restringirAEmpleado);

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
            FechaPago = _fechas.AhoraUtc(),
            Estado    = "REGISTRADO"
        };

        await using var tx = await _context.Database.BeginTransactionAsync();

        // Se guardan por separado (INSERT de payments, luego UPDATE de orders)
        // dentro de la misma transacción: mezclar ambas operaciones en un solo
        // SaveChanges producía un DbUpdateConcurrencyException espurio.
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        order.SaldoPendiente = nuevoSaldo;
        order.ActualizadoEn = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await tx.CommitAsync();

        _logger.LogInformation("Pago registrado: Pedido {Id} | Monto: {Monto} | Saldo restante: {Saldo}",
            orderId, request.Monto, nuevoSaldo);

        if (order.CustomerId != Guid.Empty)
        {
            try
            {
                var nombreCliente = order.Customer?.Nombre ?? "Cliente";
                await _notificaciones.CrearParaClienteAsync(
                    order.CustomerId,
                    "PAGO_REGISTRADO",
                    "Pago registrado en tu pedido",
                    $"Hola {nombreCliente}, se registró un pago de ${request.Monto:N2} en tu pedido. Saldo pendiente: ${nuevoSaldo:N2}.",
                    "Order", order.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al notificar PAGO_REGISTRADO para pedido {Id}", orderId);
            }
        }

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

    // ── Admin: delta incremental ─────────────────────────────────
    public async Task<IndexResultDto<OrderSummaryDto>> ListarDeltaAdminAsync(DateTime desde)
    {
        var ahora = DateTime.UtcNow;
        var items = await _context.Orders
            .Include(o => o.Customer)
            .Where(o => o.ActualizadoEn > desde)
            .OrderByDescending(o => o.FechaCreacion)
            .Select(o => new OrderSummaryDto
            {
                Id            = o.Id,
                EstadoPedido  = o.EstadoPedido,
                FechaEntrega  = o.FechaEntrega,
                Total         = o.Total,
                NombreCliente = o.Customer.Nombre,
                FechaCreacion = o.FechaCreacion,
                Archivado     = o.Archivado
            })
            .ToListAsync();

        return new IndexResultDto<OrderSummaryDto>
        {
            Items           = items,
            SincronizadoEn = ahora.ToString("o")
        };
    }

    // ── Admin: detalle ────────────────────────────────────────────
    public async Task<OrderResponseDto> ObtenerAdminAsync(Guid orderId)
        => MapToDto(await ObtenerConDetalleAsync(orderId));

    // ── Empleado: alcance recortado ───────────────────────────────
    // Este método NO acepta rango de fechas ni id de empleado desde el request.
    // Es intencional: el alcance sale del token y del reloj del servidor, así que
    // no hay parámetro que manipular para ver el día de ayer o las ventas de otro.
    public async Task<PagedResultDto<OrderSummaryDto>> ListarEmpleadoAsync(
        Guid usuarioId, string? estado, int page, int size)
    {
        var query = QueryVisiblePorEmpleado(usuarioId);

        if (!string.IsNullOrWhiteSpace(estado))
            query = query.Where(o => o.EstadoPedido == estado.ToUpper());

        return await PaginarAsync(query.OrderByDescending(o => o.FechaCreacion), page, size);
    }

    public async Task<OrderResponseDto> ObtenerParaEmpleadoAsync(Guid usuarioId, Guid orderId)
    {
        // Se comprueba la visibilidad ANTES de cargar el detalle. Si el pedido
        // existe pero no le toca, la respuesta es 404 y no 403: confirmar que el
        // id existe ya le diría al empleado algo sobre el trabajo de otro.
        var visible = await QueryVisiblePorEmpleado(usuarioId).AnyAsync(o => o.Id == orderId);
        if (!visible) throw new NotFoundException("Pedido", orderId);

        return MapToDto(await ObtenerConDetalleAsync(orderId));
    }

    /// <summary>
    /// Los pedidos que un empleado puede ver hoy: los que capturó él durante el
    /// día, más los que se entregan hoy sin importar quién los capturó.
    ///
    /// Lo segundo es lo que hace funcionar las ventas anticipadas: un pedido que
    /// se tomó la semana pasada tiene que poder moverse a EN_RUTA y ENTREGADO el
    /// día que toca, y quien lo capturó puede estar descansando. A cambio, un
    /// empleado ve el pedido ajeno del día — nada más: ni el histórico, ni los
    /// gastos, ni los cortes de nadie.
    /// </summary>
    private IQueryable<Order> QueryVisiblePorEmpleado(Guid usuarioId)
    {
        var scope = EmployeeScope.DeHoy(usuarioId, _fechas);

        return _context.Orders
            .Include(o => o.Customer)
            .Where(o => !o.Archivado)
            .Where(o => (o.AtendidoPorUsuarioId == usuarioId &&
                         o.FechaCreacion >= scope.InicioUtc &&
                         o.FechaCreacion <  scope.FinUtc)
                     || o.FechaEntrega == scope.Dia);
    }

    /// <summary>
    /// Corta el paso si el pedido cae fuera del alcance del empleado. Responde
    /// "no encontrado" en vez de "no autorizado" a propósito: decir que el id
    /// existe ya es información sobre el trabajo de otro.
    /// </summary>
    private async Task AsegurarVisibleAsync(Guid orderId, Guid? restringirAEmpleado)
    {
        if (!restringirAEmpleado.HasValue) return;

        var visible = await QueryVisiblePorEmpleado(restringirAEmpleado.Value)
            .AnyAsync(o => o.Id == orderId);

        if (!visible) throw new NotFoundException("Pedido", orderId);
    }

    // ── Helpers ───────────────────────────────────────────────────
    private async Task<OrderResponseDto> CrearPedidoAsync(
        Customer customer, string canal, string tipo,
        DateOnly fechaEntrega, TimeOnly? horaEntrega,
        DireccionDto? direccion, List<OrderItemRequestDto> items, string? notas,
        bool entregaInmediata = false, Guid? atendidoPorUsuarioId = null,
        decimal? montoPagado = null, string? metodoPago = null, decimal? costoEnvio = null,
        string? codigoCupon = null, Guid? idLocalOffline = null)
    {
        var usarPricing = canal == "WEB";
        Models.DTOs.Promotions.PricingBreakdownDto? breakdown = null;
        Dictionary<Guid, decimal>? preciosOferta = null;

        if (usarPricing)
        {
            var pricingItems = items.Select(i => new Models.DTOs.Promotions.PricingItemDto
            {
                ProductId = i.ProductId,
                Cantidad  = i.Cantidad
            }).ToList();

            breakdown = await _pricing.CalcularTotalAsync(pricingItems, codigoCupon);

            var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
            var productIds = items.Select(i => i.ProductId).Distinct().ToList();
            preciosOferta = await _context.Ofertas
                .Where(o => o.Activo
                    && productIds.Contains(o.ProductoId)
                    && (!o.FechaInicio.HasValue || o.FechaInicio <= hoy)
                    && (!o.FechaFin.HasValue    || o.FechaFin   >= hoy))
                .ToDictionaryAsync(o => o.ProductoId, o => o.PrecioOferta);
        }

        decimal total = costoEnvio ?? 0;
        var orderItems = new List<OrderItem>();

        foreach (var item in items)
        {
            var product = await _context.Products.FindAsync(item.ProductId)
                ?? throw new AppException($"Producto '{item.ProductId}' no encontrado.");
            if (product.Estado != "ACTIVO")
                throw new AppException($"El producto '{product.Nombre}' no está disponible.");

            var precioUnitario = product.PrecioBase;
            if (preciosOferta != null && preciosOferta.TryGetValue(item.ProductId, out var precioOft))
                precioUnitario = precioOft;

            if (!usarPricing)
                total += product.PrecioBase * item.Cantidad;

            orderItems.Add(new OrderItem
            {
                Id             = Guid.NewGuid(),
                ProductId      = product.Id,
                Cantidad       = item.Cantidad,
                PrecioUnitario = precioUnitario
            });
        }

        if (usarPricing)
            total += breakdown!.Total;

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
            IdLocalOffline              = idLocalOffline,
            SincronizadoEn              = idLocalOffline.HasValue ? _fechas.AhoraUtc() : null,
            CustomerId                  = customer.Id,
            TipoPedido                  = tipo.ToUpper(),
            Canal                       = canal,
            AtendidoPorUsuarioId        = atendidoPorUsuarioId,
            EstadoPedido                = entregaInmediata ? "ENTREGADO" : "PENDIENTE_VALIDACION",
            FechaCreacion               = _fechas.AhoraUtc(),
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

        // ── SALIDA inmediata: venta de mostrador ──────────────────
        // Cuando la venta es instantánea de mostrador (entregaInmediata),
        // se descuenta el inventario de la flor primaria de cada producto
        // en el momento de la venta — sin reserva. Si un producto no tiene
        // receta con flor primaria configurada, no se bloquea la venta:
        // se registra el pedido igual y se loguea una advertencia.
        if (entregaInmediata && atendidoPorUsuarioId.HasValue)
        {
            foreach (var oi in orderItems)
            {
                var receta = await _context.ProductRecipes
                    .Include(r => r.InventoryItem)
                    .Where(r => r.ProductId == oi.ProductId && r.InventoryItem.EsFlorPrimaria)
                    .FirstOrDefaultAsync();

                if (receta == null)
                {
                    _logger.LogWarning(
                        "Producto {ProductId} sin receta con flor primaria — " +
                        "se omite descuento de inventario en venta de mostrador.",
                        oi.ProductId);
                    continue;
                }

                var cantidadSalida = receta.CantidadRequerida * oi.Cantidad;
                receta.InventoryItem.StockActual -= cantidadSalida;

                _context.InventoryMovements.Add(new InventoryMovement
                {
                    Id              = Guid.NewGuid(),
                    InventoryItemId = receta.InventoryItemId,
                    TipoMovimiento  = "SALIDA",
                    Cantidad        = cantidadSalida,
                    Motivo          = $"Venta de mostrador — Pedido {order.Id}",
                    MotivoCategoria = "VENTA",
                    UsuarioId       = atendidoPorUsuarioId.Value,
                    FechaHora       = _fechas.AhoraUtc()
                });

                _logger.LogInformation(
                    "Inventario: SALIDA {Cantidad} de '{Insumo}' (producto {ProductId}) — " +
                    "venta de mostrador pedido {OrderId}",
                    cantidadSalida, receta.InventoryItem.Nombre, oi.ProductId, order.Id);
            }
        }

        if (pagado > 0 && !string.IsNullOrWhiteSpace(metodoUsado))
        {
            order.Payments.Add(new Payment
            {
                Id        = Guid.NewGuid(),
                OrderId   = order.Id,
                Monto     = pagado,
                TipoPago  = pagado >= total ? "TOTAL" : "ANTICIPO",
                Metodo    = metodoUsado.Trim().ToUpper(),
                FechaPago = _fechas.AhoraUtc(),
                Estado    = "REGISTRADO"
            });
        }

        order.ActualizadoEn = DateTime.UtcNow;
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Pedido creado: {Id} | Total: {Total} | Pagado: {Pagado}", order.Id, total, pagado);

        if (!string.IsNullOrWhiteSpace(codigoCupon))
        {
            var filas = await _context.Database.ExecuteSqlRawAsync(
                "UPDATE promotions SET usos_actuales = usos_actuales + 1 " +
                "WHERE UPPER(codigo) = {0} AND (max_usos IS NULL OR usos_actuales < max_usos)",
                codigoCupon.Trim().ToUpper());
            if (filas > 0)
                _logger.LogInformation("Cupón {Codigo} usado en pedido {Id}", codigoCupon, order.Id);
        }

        // SignalR: notificar a empleados sobre el pedido nuevo
        var direccionTexto = string.IsNullOrWhiteSpace(order.DireccionEntregaCalle)
            ? null
            : $"{order.DireccionEntregaCalle}, {order.DireccionEntregaColonia}".TrimEnd(',', ' ');
        var notifPedido = new PedidoNuevoNotificacion
        {
            OrderId       = order.Id,
            NombreCliente = $"{customer.Nombre} {customer.Apellido}".Trim(),
            TipoPedido    = order.TipoPedido,
            FechaEntrega  = order.FechaEntrega.ToString("yyyy-MM-dd"),
            HoraEntrega   = order.HoraEntrega?.ToString("HH:mm"),
            Direccion     = direccionTexto,
            Total         = order.Total,
            Notas         = order.Notas,
            EsInstantanea = entregaInmediata,
        };

        // Pedido anticipado >= 7 dias: informativo al admin, sin modal bloqueante
        if (tipo.Equals("ANTICIPADO", StringComparison.OrdinalIgnoreCase)
            && fechaEntrega >= DateOnly.FromDateTime(_fechas.AhoraUtc()).AddDays(7))
        {
            await _realtime.PedidoAnticipadoInformativoAsync(notifPedido);
        }
        else
        {
            await _realtime.PedidoNuevoAsync(notifPedido);
        }

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
