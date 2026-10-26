using Microsoft.EntityFrameworkCore;
using Npgsql;
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
using FloreriaBautista.Services.WhatsApp;

namespace FloreriaBautista.Services;

public class OrderService : IOrderService
{
    private readonly AppDbContext            _context;
    private readonly IFechaHelper            _fechas;
    private readonly IRealtimeNotifier       _realtime;
    private readonly INotificationService    _notificaciones;
    private readonly IPricingService         _pricing;
    private readonly IAvailabilityService    _availability;
    private readonly ILogger<OrderService>   _logger;
    private readonly IWhatsAppNotifier       _whatsApp;
    private readonly IAuditService           _audit;

    // Coincide con HasMaxLength de orders.motivo_cancelacion.
    private const int MotivoCancelacionMaximo = 500;

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
        INotificationService notificaciones, IPricingService pricing,
        IAvailabilityService availability, ILogger<OrderService> logger,
        IWhatsAppNotifier whatsApp, IAuditService audit)
    {
        _context        = context;
        _fechas         = fechas;
        _realtime       = realtime;
        _notificaciones = notificaciones;
        _pricing        = pricing;
        _availability   = availability;
        _logger         = logger;
        _whatsApp       = whatsApp;
        _audit          = audit;
    }

    // ── Crear pedido cliente autenticado ──────────────────────────
    public async Task<OrderResponseDto> CrearPedidoClienteAsync(Guid userId, CreateOrderRequestDto request)
    {
        ValidarTipoPedido(request.TipoPedido);

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.UserId == userId)
            ?? throw new AppException("No se encontró el perfil de cliente para este usuario.");

        return await CrearPedidoAsync(customer, "WEB", request.TipoPedido, request.FechaEntrega,
            request.HoraEntrega, request.Direccion, request.Items, request.Notas,
            costoEnvio: request.CostoEnvio, codigoCupon: request.CodigoCupon,
            solicitudVentaInstantaneaId: request.SolicitudVentaInstantaneaId);
    }

    // ── Crear pedido físico ───────────────────────────────────────
    public async Task<OrderResponseDto> CrearPedidoFisicoAsync(
        CreatePhysicalOrderRequestDto request, Guid? atendidoPorUsuarioId = null)
    {
        // Antes que nada: un tipo inválido no debe dejar ni un cliente físico creado.
        ValidarTipoPedido(request.TipoPedido);

        // Deduplicación offline-first: si ya existe un pedido con este idLocalOffline,
        // devolvemos el existente en vez de crear uno nuevo.
        if (request.IdLocalOffline.HasValue)
        {
            var existente = await BuscarPorIdLocalOfflineAsync(request.IdLocalOffline.Value);
            if (existente != null)
            {
                ValidarPropietarioVenta(existente, atendidoPorUsuarioId);
                return MapToDto(existente);
            }
        }

        // Después de la idempotencia (un reenvío devuelve la venta aunque su fecha
        // ya haya pasado) y antes de crear el cliente físico.
        ValidarFechaEntrega(request.TipoPedido, request.FechaEntrega);

        // ── Fecha de captura (venta offline) ──────────────────────────
        // Va después de la idempotencia: reenviar una venta ya registrada devuelve
        // la misma aunque su captura haya salido de la ventana.
        var (capturadoEn, fechaContable) =
            await ResolverCapturaAsync(request.CapturadoEn, atendidoPorUsuarioId);

        var esInstantaneo = request.TipoPedido.Trim().ToUpper() == "INSTANTANEO";

        // ── Existencias de la venta de mostrador ──────────────────────
        // Va después de la idempotencia (reenviar una venta ya registrada
        // devuelve el mismo pedido) y antes de tocar el contexto. Se suma por
        // insumo porque un insumo puede repetirse entre productos. Productos
        // sin receta no se validan. Una venta sincronizada sin conexión se
        // acepta igual (las flores ya se entregaron) y deja constancia.
        Dictionary<Guid, List<ProductRecipe>>? recetasPorProducto = null;
        var faltantes = new List<(InventoryItem Insumo, int Necesario)>();
        if (esInstantaneo && atendidoPorUsuarioId.HasValue)
        {
            var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
            recetasPorProducto = (await _context.ProductRecipes
                    .Include(r => r.InventoryItem)
                    .Where(r => productIds.Contains(r.ProductId))
                    .ToListAsync())
                .GroupBy(r => r.ProductId)
                .ToDictionary(g => g.Key, g => g.ToList());

            faltantes = request.Items
                .SelectMany(i => recetasPorProducto.TryGetValue(i.ProductId, out var rs)
                    ? rs.Select(r => (r.InventoryItem, Cantidad: r.CantidadRequerida * i.Cantidad))
                    : [])
                .GroupBy(x => x.InventoryItem)
                .Select(g => (Insumo: g.Key, Necesario: g.Sum(x => x.Cantidad)))
                .Where(x => x.Necesario > x.Insumo.StockActual)
                .ToList();

            if (faltantes.Count > 0 && !request.VentaSincronizadaSinConexion)
                throw new AppException(string.Join(" ", faltantes.Select(f =>
                    $"No hay existencias suficientes de '{f.Insumo.Nombre}': " +
                    $"hay {f.Insumo.StockActual} y se necesitan {f.Necesario}.")));
        }

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

        OrderResponseDto pedido;
        try
        {
            pedido = await CrearPedidoAsync(customer, "FISICO", request.TipoPedido, request.FechaEntrega,
                request.HoraEntrega, request.Direccion, request.Items, request.Notas,
                entregaInmediata: esInstantaneo,
                atendidoPorUsuarioId: atendidoPorUsuarioId,
                montoPagado: request.MontoPagado, metodoPago: request.MetodoPago,
                costoEnvio: request.CostoEnvio,
                idLocalOffline: request.IdLocalOffline,
                recetasPorProducto: recetasPorProducto,
                capturadoEn: capturadoEn, fechaContable: fechaContable,
                totalCapturado: request.TotalCapturado);
        }
        catch (DbUpdateException ex) when (request.IdLocalOffline.HasValue && EsColisionIdLocalOffline(ex))
        {
            // Reenvío simultáneo: otra petición guardó esta venta entre la consulta
            // de arriba y este SaveChanges. El guardado fallido es atómico (nada
            // quedó escrito ni se notificó), pero el contexto conserva el pedido
            // en Added y el stock modificado: se descartan antes de releer.
            _context.ChangeTracker.Clear();

            var existente = await BuscarPorIdLocalOfflineAsync(request.IdLocalOffline.Value);
            if (existente == null) throw;

            ValidarPropietarioVenta(existente, atendidoPorUsuarioId);
            _logger.LogInformation(
                "Venta duplicada concurrente {IdLocalOffline}: se devuelve el pedido {Id}",
                request.IdLocalOffline, existente.Id);
            return MapToDto(existente);
        }

        // Venta offline aceptada sin existencias: constancia para que el admin ajuste el inventario.
        foreach (var f in faltantes)
        {
            // StockActual ya refleja la SALIDA; el stock al vender era el de antes.
            var stockAntes = f.Insumo.StockActual + f.Necesario;
            _logger.LogWarning(
                "Venta sin conexión sincronizada sin existencias: pedido {OrderId} | insumo '{Insumo}' " +
                "| stock {Stock} | necesario {Necesario} | usuario {UsuarioId}",
                pedido.Id, f.Insumo.Nombre, stockAntes, f.Necesario, atendidoPorUsuarioId);

            await _audit.RegistrarAsync(
                accion:    "VENTA_OFFLINE_SIN_EXISTENCIAS",
                entidad:   "Order",
                entidadId: pedido.Id.ToString(),
                usuarioId: atendidoPorUsuarioId,
                detalles:  new
                {
                    InventoryItemId = f.Insumo.Id,
                    Insumo          = f.Insumo.Nombre,
                    Stock           = stockAntes,
                    Necesario       = f.Necesario,
                    request.IdLocalOffline
                });
        }

        return pedido;
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
    /// <param name="usuarioActor">
    /// Quien hace el cambio (admin o empleado). Firma los movimientos de
    /// inventario cuando no hay restricción de empleado.
    /// </param>
    public async Task<OrderResponseDto> CambiarEstadoAsync(
        Guid orderId, UpdateOrderStatusRequestDto request, List<string> rolesUsuario,
        Guid? restringirAEmpleado = null, Guid? usuarioActor = null)
    {
        await AsegurarVisibleAsync(orderId, restringirAEmpleado);

        var order = await ObtenerConDetalleAsync(orderId);
        var nuevoEstado    = request.NuevoEstado.ToUpper();
        var estadoAnterior = order.EstadoPedido;

        // Cancelar, pedir la anulación o resolverla es decisión del admin. Va antes
        // que la transición: primero el permiso, luego si el cambio es posible.
        var esAdmin = rolesUsuario.Contains("ADMIN", StringComparer.OrdinalIgnoreCase);
        if (!esAdmin && (nuevoEstado is "CANCELADO" or "PENDIENTE_ANULACION"
                         || estadoAnterior == "PENDIENTE_ANULACION"))
            throw new ForbiddenException(
                "Solo el administrador puede cancelar o anular un pedido. Usa «Reportar un error» para pedirlo.");

        if (!Transiciones.TryGetValue(estadoAnterior, out var permitidos) ||
            !permitidos.Contains(nuevoEstado))
            throw new AppException(
                $"No se puede cambiar de '{estadoAnterior}' a '{nuevoEstado}'.");

        // Ninguna cancelación sin motivo, venga de la web, del celular o de la API.
        // Se valida antes de tocar el pedido o el inventario.
        string? motivoCancelacion = null;
        if (nuevoEstado == "CANCELADO")
        {
            motivoCancelacion = request.MotivoCancelacion?.Trim();
            if (string.IsNullOrEmpty(motivoCancelacion))
                throw new AppException("Indica el motivo de la cancelación.");
            if (motivoCancelacion.Length > MotivoCancelacionMaximo)
                throw new AppException(
                    $"El motivo de la cancelación no puede pasar de {MotivoCancelacionMaximo} caracteres.");
            order.MotivoCancelacion = motivoCancelacion;
        }

        order.EstadoPedido = nuevoEstado;
        if (!string.IsNullOrWhiteSpace(request.Notas))
            order.Notas = request.Notas;

        // Un pedido web o telefónico llega sin dueño; quien lo saca de
        // PENDIENTE_VALIDACION es quien lo atendió. Solo se escribe si estaba
        // vacío: el primero que lo tocó se queda con la atribución.
        if (order.AtendidoPorUsuarioId is null && restringirAEmpleado.HasValue)
            order.AtendidoPorUsuarioId = restringirAEmpleado;

        // El admin no se queda con «atendido por», pero sí firma el movimiento.
        var usuarioMovimiento = restringirAEmpleado ?? usuarioActor ?? order.AtendidoPorUsuarioId;
        const string sinUsuarioMovimiento =
            "No se pudo determinar el usuario que registra el movimiento de inventario.";

        // Un pedido de venta instantánea (con solicitud vinculada) descuenta su
        // receta una sola vez, al liquidarse (RegistrarPagoAsync); nunca al preparar.
        var esVentaInstantanea = await _context.SolicitudesVentaInstantanea
            .AnyAsync(s => s.OrderId == order.Id);
        var yaDescontoInventario = esVentaInstantanea
            ? order.SaldoPendiente == 0
            : estadoAnterior == "EN_PREPARACION" || estadoAnterior == "EN_RUTA";

        // Una sola marca para el pedido y los insumos que toca: el delta de
        // inventario solo trae lo que tiene ActualizadoEn posterior a 'desde'.
        var ahora = _fechas.AhoraUtc();

        // ── Descuento de inventario al entrar en preparación (pedidos no instantáneos) ──
        if (nuevoEstado == "EN_PREPARACION" && estadoAnterior == "PENDIENTE_VALIDACION" && !esVentaInstantanea)
        {
            var usuarioMov = usuarioMovimiento ?? throw new AppException(sinUsuarioMovimiento);
            foreach (var oi in order.OrderItems)
            {
                var recetas = await _context.ProductRecipes
                    .Include(r => r.InventoryItem)
                    .Where(r => r.ProductId == oi.ProductId)
                    .ToListAsync();

                foreach (var receta in recetas)
                {
                    var cantidadSalida = receta.CantidadRequerida * oi.Cantidad;
                    receta.InventoryItem.StockActual -= cantidadSalida;
                    receta.InventoryItem.ActualizadoEn = ahora;

                    _context.InventoryMovements.Add(new InventoryMovement
                    {
                        Id              = Guid.NewGuid(),
                        InventoryItemId = receta.InventoryItemId,
                        TipoMovimiento  = "SALIDA",
                        Cantidad        = cantidadSalida,
                        Motivo          = $"Preparación — Pedido {orderId}",
                        MotivoCategoria = "VENTA",
                        UsuarioId       = usuarioMov,
                        FechaHora       = _fechas.AhoraUtc()
                    });
                }
            }
        }

        // ── Devolución de inventario al cancelar un pedido que ya descontó ──
        if (nuevoEstado == "CANCELADO" && yaDescontoInventario)
        {
            var usuarioMov = usuarioMovimiento ?? throw new AppException(sinUsuarioMovimiento);
            foreach (var oi in order.OrderItems)
            {
                var recetas = await _context.ProductRecipes
                    .Include(r => r.InventoryItem)
                    .Where(r => r.ProductId == oi.ProductId)
                    .ToListAsync();

                foreach (var receta in recetas)
                {
                    var cantidadDevuelta = receta.CantidadRequerida * oi.Cantidad;
                    receta.InventoryItem.StockActual += cantidadDevuelta;
                    receta.InventoryItem.ActualizadoEn = ahora;

                    _context.InventoryMovements.Add(new InventoryMovement
                    {
                        Id              = Guid.NewGuid(),
                        InventoryItemId = receta.InventoryItemId,
                        TipoMovimiento  = "ENTRADA",
                        Cantidad        = cantidadDevuelta,
                        Motivo          = $"Devolución por cancelación — Pedido {orderId}",
                        MotivoCategoria = "DEVOLUCION",
                        UsuarioId       = usuarioMov,
                        FechaHora       = _fechas.AhoraUtc()
                    });
                }
            }
        }

        order.ActualizadoEn = ahora;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Pedido {Id}: {Anterior} → {Nuevo}", orderId, estadoAnterior, nuevoEstado);

        // Los nombres en minúscula los lee PeopleReportsService (tiempo de preparación).
        await _audit.RegistrarAsync(
            accion:    "CAMBIO_ESTADO",
            entidad:   "Order",
            entidadId: order.Id.ToString(),
            usuarioId: usuarioActor ?? restringirAEmpleado,
            detalles:  new { anterior = estadoAnterior, nuevo = nuevoEstado, motivo = motivoCancelacion });

        await AvisarCambioEstadoWhatsAppAsync(order, estadoAnterior, nuevoEstado);

        // El empleado que atendió el pedido se entera si otra persona lo movió.
        var actor = restringirAEmpleado ?? usuarioActor;
        if (order.AtendidoPorUsuarioId is Guid atendio && atendio != actor)
            await _realtime.PedidoCambioEstadoAsync(order.Id, atendio, nuevoEstado);

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
        Guid orderId, RegisterPaymentRequestDto request, Guid? restringirAEmpleado = null,
        Guid? usuarioActor = null)
    {
        await AsegurarVisibleAsync(orderId, restringirAEmpleado);

        // Idempotencia: un reintento con la misma clave devuelve el pedido tal
        // como quedó. Va antes de validar el saldo; si no, reintentar una
        // liquidación fallaría con «excede el saldo».
        var idLocalPago = request.IdLocalOffline is Guid g && g != Guid.Empty ? g : (Guid?)null;
        if (idLocalPago.HasValue)
        {
            var yaRegistrado = await ResolverPagoRepetidoAsync(orderId, idLocalPago.Value);
            if (yaRegistrado != null) return yaRegistrado;
        }

        var order = await ObtenerConDetalleAsync(orderId);

        if (order.EstadoPedido == "CANCELADO")
            throw new AppException("No se pueden registrar pagos sobre un pedido cancelado.");

        if (request.Monto > order.SaldoPendiente)
            throw new AppException(
                $"El monto (${request.Monto}) excede el saldo pendiente (${order.SaldoPendiente}).");

        var saldoAnterior = order.SaldoPendiente;
        var nuevoSaldo = order.SaldoPendiente - request.Monto;

        var payment = new Payment
        {
            Id        = Guid.NewGuid(),
            OrderId   = order.Id,
            Monto     = request.Monto,
            TipoPago  = nuevoSaldo == 0 ? "LIQUIDACION" : "ANTICIPO",
            Metodo    = request.Metodo.Trim().ToUpper(),
            FechaPago = _fechas.AhoraUtc(),
            Estado    = "REGISTRADO",
            IdLocalOffline         = idLocalPago,
            RegistradoPorUsuarioId = usuarioActor
        };

        await using var tx = await _context.Database.BeginTransactionAsync();

        // Se guardan por separado (INSERT de payments, luego UPDATE de orders)
        // dentro de la misma transacción: mezclar ambas operaciones en un solo
        // SaveChanges producía un DbUpdateConcurrencyException espurio.
        _context.Payments.Add(payment);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (idLocalPago.HasValue && EsColisionIdLocalPago(ex))
        {
            // Reintento simultáneo: otra petición guardó este abono entre la
            // consulta de arriba y este INSERT. Nada quedó escrito; se descarta
            // la transacción y lo que el contexto conserva antes de releer.
            await tx.RollbackAsync();
            _context.ChangeTracker.Clear();

            var existente = await ResolverPagoRepetidoAsync(orderId, idLocalPago.Value);
            if (existente == null) throw;

            _logger.LogInformation(
                "Abono duplicado concurrente {IdLocalOffline}: se devuelve el pedido {Id}",
                idLocalPago, orderId);
            return existente;
        }

        // Misma marca para el pedido y, si se liquida una venta instantánea, sus insumos.
        var ahora = _fechas.AhoraUtc();
        order.SaldoPendiente = nuevoSaldo;
        order.ActualizadoEn = ahora;
        await _context.SaveChangesAsync();

        // ── Confirmar reserva de venta instantánea al liquidar ──────
        if (nuevoSaldo == 0)
        {
            var solicitudVinculada = await _context.SolicitudesVentaInstantanea
                .FirstOrDefaultAsync(s => s.OrderId == orderId && s.Estado == "ACEPTADA");

            if (solicitudVinculada != null)
            {
                var orderItemsConReceta = await _context.OrderItems
                    .Where(oi => oi.OrderId == orderId)
                    .ToListAsync();

                foreach (var oi in orderItemsConReceta)
                {
                    var recetas = await _context.ProductRecipes
                        .Include(r => r.InventoryItem)
                        .Where(r => r.ProductId == oi.ProductId)
                        .ToListAsync();

                    if (recetas.Count == 0)
                    {
                        _logger.LogWarning(
                            "Producto {ProductId} sin receta — " +
                            "se omite descuento de inventario en confirmación de pago de venta instantánea.",
                            oi.ProductId);
                        continue;
                    }

                    var usuarioMovimiento = solicitudVinculada.DecididaPorUsuarioId
                        ?? order.AtendidoPorUsuarioId
                        ?? Guid.Empty;

                    foreach (var receta in recetas)
                    {
                        var cantidadSalida = receta.CantidadRequerida * oi.Cantidad;
                        receta.InventoryItem.StockActual -= cantidadSalida;
                        receta.InventoryItem.ActualizadoEn = ahora;

                        _context.InventoryMovements.Add(new InventoryMovement
                        {
                            Id              = Guid.NewGuid(),
                            InventoryItemId = receta.InventoryItemId,
                            TipoMovimiento  = "SALIDA",
                            Cantidad        = cantidadSalida,
                            Motivo          = $"Confirmación de pago — Venta instantánea pedido {orderId}",
                            MotivoCategoria = "VENTA",
                            UsuarioId       = usuarioMovimiento,
                            FechaHora       = _fechas.AhoraUtc()
                        });

                        _logger.LogInformation(
                            "Inventario: SALIDA {Cantidad} de '{Insumo}' (producto {ProductId}) — " +
                            "confirmación pago venta instantánea pedido {OrderId}",
                            cantidadSalida, receta.InventoryItem.Nombre, oi.ProductId, orderId);
                    }
                }

                await _context.SaveChangesAsync();
            }
        }

        await tx.CommitAsync();

        // Fuera de la transacción: AuditService guarda por su cuenta. Sin usuario
        // es un pago que acreditó el sistema (webhook de Mercado Pago).
        await _audit.RegistrarAsync(
            accion:    "PAGO_REGISTRADO",
            entidad:   "Order",
            entidadId: orderId.ToString(),
            usuarioId: usuarioActor ?? restringirAEmpleado,
            detalles:  new
            {
                paymentId = payment.Id,
                monto     = payment.Monto,
                metodo    = payment.Metodo,
                tipoPago  = payment.TipoPago,
                saldoAnterior,
                saldoNuevo = nuevoSaldo
            });

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
    public Task<IndexResultDto<OrderSummaryDto>> ListarDeltaAdminAsync(DateTime desde)
        => ListarDeltaAsync(_context.Orders.Include(o => o.Customer), desde);

    // ── Empleado: delta incremental ──────────────────────────────
    // Parte del mismo alcance que el listado y el detalle del empleado: nunca
    // debe recibir por el delta un pedido que no podría abrir. Limitación
    // conocida: no informa de los pedidos que salen de su alcance (cambio de
    // día o archivado); informar esos ids sería volver a filtrar información.
    public Task<IndexResultDto<OrderSummaryDto>> ListarDeltaEmpleadoAsync(Guid usuarioId, DateTime desde)
        => ListarDeltaAsync(QueryVisiblePorEmpleado(usuarioId), desde);

    private static async Task<IndexResultDto<OrderSummaryDto>> ListarDeltaAsync(
        IQueryable<Order> query, DateTime desde)
    {
        var ahora = DateTime.UtcNow;
        var items = await query
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
    // Mismos valores que el CHECK ck_ord_tipo de la tabla orders.
    private static readonly HashSet<string> TiposPedido = ["INSTANTANEO", "ANTICIPADO"];

    private static void ValidarTipoPedido(string? tipo)
    {
        if (!TiposPedido.Contains(tipo?.Trim().ToUpper() ?? ""))
            throw new AppException("Tipo de pedido no válido. Valores admitidos: INSTANTANEO, ANTICIPADO.");
    }

    private async Task<OrderResponseDto> CrearPedidoAsync(
        Customer customer, string canal, string tipo,
        DateOnly fechaEntrega, TimeOnly? horaEntrega,
        DireccionDto? direccion, List<OrderItemRequestDto> items, string? notas,
        bool entregaInmediata = false, Guid? atendidoPorUsuarioId = null,
        decimal? montoPagado = null, string? metodoPago = null, decimal? costoEnvio = null,
        string? codigoCupon = null, Guid? idLocalOffline = null,
        Guid? solicitudVentaInstantaneaId = null,
        Dictionary<Guid, List<ProductRecipe>>? recetasPorProducto = null,
        DateTime? capturadoEn = null, DateTime? fechaContable = null,
        decimal? totalCapturado = null)
    {
        ValidarFechaEntrega(tipo, fechaEntrega);

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

        // El servidor recalcula con precios vigentes; si no coincide con lo que el
        // empleado cobró al capturar, la venta no se registra y se avisa al admin.
        if (totalCapturado.HasValue && Math.Round(totalCapturado.Value, 2) != Math.Round(total, 2))
            await RechazarTotalCapturadoAsync(totalCapturado.Value, total, atendidoPorUsuarioId, idLocalOffline);

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

        // ── Validación de solicitud de venta instantánea ─────────────
        SolicitudVentaInstantanea? solicitud = null;
        if (solicitudVentaInstantaneaId.HasValue)
        {
            solicitud = await _context.SolicitudesVentaInstantanea
                .FirstOrDefaultAsync(s => s.Id == solicitudVentaInstantaneaId.Value);

            if (solicitud == null)
                throw new ConflictException("La solicitud de venta instantánea no existe.");

            if (solicitud.Estado != "ACEPTADA")
                throw new ConflictException("La solicitud de venta instantánea no está aceptada.");

            if (solicitud.ReservaExpiraEn.HasValue && solicitud.ReservaExpiraEn.Value < _fechas.AhoraUtc())
                throw new ConflictException("La reserva de tu solicitud de venta instantánea ha expirado. Vuelve a intentarlo.");

            if (solicitud.CustomerId != customer.Id)
                throw new ConflictException("La solicitud de venta instantánea no pertenece a este cliente.");

            if (solicitud.OrderId.HasValue)
                throw new ConflictException("Esta solicitud de venta instantánea ya tiene un pedido asociado.");

            // El pedido debe ser exactamente lo aprobado: un solo producto (aunque venga
            // en varias líneas) y la misma cantidad total.
            var lineasPorProducto = items
                .GroupBy(i => i.ProductId)
                .Select(g => new { ProductId = g.Key, Cantidad = g.Sum(i => i.Cantidad) })
                .ToList();
            if (lineasPorProducto.Count != 1
                || lineasPorProducto[0].ProductId != solicitud.ProductId
                || lineasPorProducto[0].Cantidad != solicitud.Cantidad)
                throw new ConflictException("El pedido debe ser del producto y la cantidad aprobados en tu solicitud.");
        }

        // ── Validación de venta anticipada ────────────────────────────
        if (tipo.Equals("ANTICIPADO", StringComparison.OrdinalIgnoreCase) && !entregaInmediata)
        {
            foreach (var item in items)
            {
                var validacion = await _availability.ValidarVentaAnticipadaAsync(
                    item.ProductId, item.Cantidad, fechaEntrega);

                if (!validacion.PuedeAceptarse)
                {
                    foreach (var ins in validacion.InsumosPorAbastecer.Where(i => i.Estado == "SIN_TIEMPO"))
                    {
                        await _availability.RegistrarDemandaNoCubiertaAsync(
                            ins.InventoryItemId, item.ProductId,
                            ins.CantidadFaltante, 0, "VENTA_ANTICIPADA");
                    }

                    var fechaSugerida = validacion.FechaViableSugerida?.ToString("yyyy-MM-dd") ?? "N/A";
                    throw new AppException(
                        $"No hay disponibilidad suficiente para '{item.ProductId}' en la fecha solicitada. " +
                        $"{validacion.Mensaje} Fecha sugerida: {fechaSugerida}.");
                }
            }
        }

        var order = new Order
        {
            Id                          = Guid.NewGuid(),
            IdLocalOffline              = idLocalOffline,
            SincronizadoEn              = idLocalOffline.HasValue ? _fechas.AhoraUtc() : null,
            CustomerId                  = customer.Id,
            TipoPedido                  = tipo.Trim().ToUpper(),
            Canal                       = canal,
            AtendidoPorUsuarioId        = atendidoPorUsuarioId,
            EstadoPedido                = entregaInmediata ? "ENTREGADO" : "PENDIENTE_VALIDACION",
            // Venta offline: FechaCreacion es el día contable (el de captura si su
            // corte sigue abierto); CapturadoEn conserva el instante original.
            FechaCreacion               = fechaContable ?? _fechas.AhoraUtc(),
            CapturadoEn                 = capturadoEn,
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

        // Misma marca para el pedido y los insumos que descuenta la venta de mostrador.
        var ahora = _fechas.AhoraUtc();

        // ── SALIDA inmediata: venta de mostrador ──────────────────
        // Cuando la venta es instantánea de mostrador (entregaInmediata),
        // se descontán TODOS los insumos de la receta de cada producto
        // en el momento de la venta. Si un producto no tiene receta, no se
        // bloquea la venta: se registra el pedido y se loguea una advertencia.
        if (entregaInmediata && atendidoPorUsuarioId.HasValue)
        {
            foreach (var oi in orderItems)
            {
                // Las recetas ya vienen cargadas de la validación de existencias.
                var recetas = recetasPorProducto != null
                    ? recetasPorProducto.GetValueOrDefault(oi.ProductId) ?? []
                    : await _context.ProductRecipes
                        .Include(r => r.InventoryItem)
                        .Where(r => r.ProductId == oi.ProductId)
                        .ToListAsync();

                if (recetas.Count == 0)
                {
                    _logger.LogWarning(
                        "Producto {ProductId} sin receta — " +
                        "se omite descuento de inventario en venta de mostrador.",
                        oi.ProductId);
                    continue;
                }

                foreach (var receta in recetas)
                {
                    var cantidadSalida = receta.CantidadRequerida * oi.Cantidad;
                    receta.InventoryItem.StockActual -= cantidadSalida;
                    receta.InventoryItem.ActualizadoEn = ahora;

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
                // El corte cuadra el efectivo por FechaPago: va al mismo día que la venta.
                FechaPago = fechaContable ?? _fechas.AhoraUtc(),
                Estado    = "REGISTRADO",
                RegistradoPorUsuarioId = atendidoPorUsuarioId
            });
        }

        order.ActualizadoEn = ahora;
        _context.Orders.Add(order);

        if (solicitud == null)
        {
            await _context.SaveChangesAsync();
        }
        else
        {
            // ── Vincular solicitud de venta instantánea al Order ─────────
            // Se reclama con una actualización condicional en la misma transacción
            // que inserta el pedido: si dos peticiones llegan a la vez, la segunda
            // afecta 0 filas y su pedido se descarta. El pedido va primero porque
            // order_id es FK a orders.
            await using var tx = await _context.Database.BeginTransactionAsync();
            await _context.SaveChangesAsync();

            var vinculadas = await _context.SolicitudesVentaInstantanea
                .Where(s => s.Id == solicitud.Id && s.OrderId == null && s.Estado == "ACEPTADA")
                .ExecuteUpdateAsync(set => set.SetProperty(s => s.OrderId, order.Id));

            if (vinculadas == 0)
            {
                await tx.RollbackAsync();
                var sigueAceptada = await _context.SolicitudesVentaInstantanea
                    .AsNoTracking()
                    .AnyAsync(s => s.Id == solicitud.Id && s.Estado == "ACEPTADA");
                throw new ConflictException(sigueAceptada
                    ? "Esta solicitud de venta instantánea ya tiene un pedido asociado."
                    : "La solicitud de venta instantánea no está aceptada.");
            }

            await tx.CommitAsync();
        }

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
            await AvisarPedidoNuevoWhatsAppAsync(notifPedido, anticipado: true);
        }
        else
        {
            await _realtime.PedidoNuevoAsync(notifPedido);
            await AvisarPedidoNuevoWhatsAppAsync(notifPedido, anticipado: false);
        }

        return MapToDto(await ObtenerConDetalleAsync(order.Id));
    }

    // ── Avisos por WhatsApp (encolado; el envío lo hace el despachador) ──
    // Van después del SaveChanges y nunca cambian la respuesta de la operación.
    private async Task AvisarPedidoNuevoWhatsAppAsync(PedidoNuevoNotificacion n, bool anticipado)
    {
        try
        {
            // Datos mínimos: sin dirección ni teléfono del cliente.
            var entrega = string.IsNullOrEmpty(n.HoraEntrega) ? n.FechaEntrega : $"{n.FechaEntrega} {n.HoraEntrega}";
            var texto = $"{(anticipado ? "(Anticipado) " : "")}🛒 Pedido nuevo #{WhatsAppConstantes.IdCorto(n.OrderId)}\n" +
                        $"Cliente: {n.NombreCliente}\n" +
                        $"Tipo: {n.TipoPedido}\n" +
                        $"Entrega: {entrega}\n" +
                        $"Total: ${n.Total:N2}";
            await _whatsApp.EncolarAsync(WhatsAppConstantes.EventoPedidoNuevo, texto, "Order", n.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al encolar WhatsApp de pedido nuevo {Id}", n.OrderId);
        }
    }

    private async Task AvisarCambioEstadoWhatsAppAsync(Order order, string anterior, string nuevo)
    {
        try
        {
            var s = await _context.WhatsAppSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1);
            if (s is null || !s.Habilitado || !s.NotificarCambioEstado) return;

            var estados = s.EstadosNotificados.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (!estados.Contains(nuevo, StringComparer.OrdinalIgnoreCase)) return;

            var cliente = order.Customer is null ? "Cliente" : $"{order.Customer.Nombre} {order.Customer.Apellido}".Trim();
            var texto = $"📦 Pedido #{WhatsAppConstantes.IdCorto(order.Id)} ({cliente}): " +
                        $"{WhatsAppConstantes.EstadoLegible(anterior)} → {WhatsAppConstantes.EstadoLegible(nuevo)}";
            await _whatsApp.EncolarAsync(WhatsAppConstantes.EventoPedidoEstado, texto, "Order", order.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al encolar WhatsApp de cambio de estado del pedido {Id}", order.Id);
        }
    }

    private async Task<Order> ObtenerConDetalleAsync(Guid id)
        => await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == id)
            ?? throw new NotFoundException("Pedido", id);

    // ── Idempotencia de la venta de mostrador (IdLocalOffline) ────
    private const string IndiceIdLocalOffline = "ix_orders_id_local_offline";

    private Task<Order?> BuscarPorIdLocalOfflineAsync(Guid idLocalOffline)
        => _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.IdLocalOffline == idLocalOffline);

    // Un reenvío solo devuelve la venta a quien la registró. Pedidos viejos sin
    // atribución (o llamadas sin usuario) se devuelven como siempre.
    private static void ValidarPropietarioVenta(Order existente, Guid? atendidoPorUsuarioId)
    {
        if (existente.AtendidoPorUsuarioId.HasValue && atendidoPorUsuarioId.HasValue
            && existente.AtendidoPorUsuarioId != atendidoPorUsuarioId)
            throw new ConflictException("Esta venta ya fue registrada por otro usuario.");
    }

    /// <summary>
    /// Valida la captura offline y decide el instante contable de la venta: el de
    /// captura si el corte de ese empleado para ese día sigue abierto; null (= ahora,
    /// corte vigente) si ya se cerró. Sin captura, ambos son null y nada cambia.
    /// </summary>
    private async Task<(DateTime? CapturadoEn, DateTime? FechaContable)> ResolverCapturaAsync(
        DateTime? capturadoEn, Guid? usuarioId)
    {
        if (!capturadoEn.HasValue) return (null, null);

        var captura = CapturaOffline.Validar(capturadoEn.Value, _fechas.AhoraUtc());
        if (!usuarioId.HasValue) return (captura, captura);

        var diaCaptura   = DateOnly.FromDateTime(_fechas.ALocal(captura));
        var corteCerrado = await _context.CashCuts.AnyAsync(c => c.UsuarioId  == usuarioId.Value
                                                              && c.FechaLocal == diaCaptura
                                                              && c.Estado     != "ANULADO");
        return (captura, corteCerrado ? null : captura);
    }

    private async Task RechazarTotalCapturadoAsync(
        decimal totalCapturado, decimal totalVigente, Guid? usuarioId, Guid? idLocalOffline)
    {
        _logger.LogWarning(
            "Venta rechazada por total distinto: capturado {Capturado} | vigente {Vigente} " +
            "| usuario {UsuarioId} | idLocalOffline {IdLocalOffline}",
            totalCapturado, totalVigente, usuarioId, idLocalOffline);

        // El aviso no debe ocultar el rechazo: si falla, solo queda en el log.
        try
        {
            var empleado = usuarioId.HasValue
                ? await _context.Users.Where(u => u.Id == usuarioId.Value).Select(u => u.Nombre).FirstOrDefaultAsync()
                : null;
            await _notificaciones.CrearParaUsuariosAsync(
                "VENTA_REQUIERE_ATENCION",
                "Venta sin conexión por revisar",
                $"{empleado ?? "Un empleado"} intentó sincronizar una venta de ${totalCapturado:N2} " +
                $"cuyo total con precios vigentes es ${totalVigente:N2}. Quedó pendiente en su dispositivo.",
                ["ADMIN"]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo avisar al admin de la venta con total distinto");
        }

        throw new ConflictException(
            $"El total de la venta cambió: cobraste ${totalCapturado:N2} y con los precios vigentes " +
            $"es ${totalVigente:N2}. Revisa la venta antes de enviarla de nuevo.");
    }

    // Solo la violación del índice único de IdLocalOffline; cualquier otro error se propaga.
    private static bool EsColisionIdLocalOffline(DbUpdateException ex)
        => ex.InnerException is PostgresException pg
           && pg.SqlState == PostgresErrorCodes.UniqueViolation
           && pg.ConstraintName == IndiceIdLocalOffline;

    // ── Idempotencia de los abonos (Payment.IdLocalOffline) ───────
    private const string IndiceIdLocalPago = "ix_payments_id_local_offline";

    // Null si la clave no se ha usado. Si ya se usó en este pedido, el pedido
    // tal como quedó; si se usó en otro pedido, conflicto.
    private async Task<OrderResponseDto?> ResolverPagoRepetidoAsync(Guid orderId, Guid idLocalPago)
    {
        var pedidoDelPago = await _context.Payments
            .Where(p => p.IdLocalOffline == idLocalPago)
            .Select(p => (Guid?)p.OrderId)
            .FirstOrDefaultAsync();

        if (pedidoDelPago == null) return null;
        if (pedidoDelPago != orderId)
            throw new ConflictException("Este abono ya se registró en otro pedido.");

        return MapToDto(await ObtenerConDetalleAsync(orderId));
    }

    private static bool EsColisionIdLocalPago(DbUpdateException ex)
        => ex.InnerException is PostgresException pg
           && pg.SqlState == PostgresErrorCodes.UniqueViolation
           && pg.ConstraintName == IndiceIdLocalPago;

    // Un pedido anticipado no puede programarse para un día que ya pasó en la
    // tienda. Hoy sí vale. La venta de mostrador se entrega al momento.
    private void ValidarFechaEntrega(string? tipo, DateOnly fechaEntrega)
    {
        if (string.Equals(tipo?.Trim(), "ANTICIPADO", StringComparison.OrdinalIgnoreCase)
            && fechaEntrega < _fechas.HoyLocal())
            throw new AppException("La fecha de entrega no puede ser anterior a hoy.");
    }

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
        MotivoCancelacion = o.MotivoCancelacion,
        NombreCliente  = o.Customer?.Nombre ?? "",
        FechaCreacion  = o.FechaCreacion,
        CapturadoEn    = o.CapturadoEn,
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
