using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.InstantSales;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Services.Interfaces;
using FloreriaBautista.Services.Realtime;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Services;

public class InstantSaleService : IInstantSaleService
{
    // -- Timeouts configurables (Pregunta 8 del plan completo) ------
    // Se mantienen como constantes en un solo lugar; en produccion se
    // pueden mover a appsettings.json sin tocar multiples archivos.
    public static readonly TimeSpan ReservaDuracion = TimeSpan.FromMinutes(10);

    private readonly AppDbContext                _context;
    private readonly IFechaHelper                _fechas;
    private readonly IAuditService               _audit;
    private readonly IRealtimeNotifier           _realtime;
    private readonly ILogger<InstantSaleService> _logger;

    public InstantSaleService(
        AppDbContext context,
        IFechaHelper fechas,
        IAuditService audit,
        IRealtimeNotifier realtime,
        ILogger<InstantSaleService> logger)
    {
        _context  = context;
        _fechas   = fechas;
        _audit    = audit;
        _realtime = realtime;
        _logger   = logger;
    }

    public async Task<InstantSaleResponseDto> CrearSolicitudAsync(
        Guid customerId,
        CreateInstantSaleRequestDto request)
    {
        // ── Validaciones basicas ──────────────────────────────────────
        var product = await _context.Products
            .Include(p => p.ProductRecipes)
                .ThenInclude(pr => pr.InventoryItem)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId);

        if (product == null)
            throw new NotFoundException("Producto", request.ProductId);

        if (product.Estado != "ACTIVO")
            throw new AppException("El producto no esta activo.");

        // ── Rechazo automatico: producto no habilitado ────────────────
        if (!product.PermiteVentaInstantanea)
        {
            var rechazada = new SolicitudVentaInstantanea
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                ProductId = request.ProductId,
                Cantidad = request.Cantidad,
                Estado = "RECHAZADA",
                MotivoRechazo = "Este producto requiere al menos una semana de anticipación. Cualquier duda, acude a tu sucursal.",
                CreadaEn = DateTime.UtcNow,
                DecididaEn = DateTime.UtcNow,
            };
            _context.SolicitudesVentaInstantanea.Add(rechazada);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Solicitud {Id} rechazada automaticamente: producto {ProductId} no permite venta instantanea",
                rechazada.Id, request.ProductId);

            return MapToDto(rechazada, product.Nombre);
        }

        // ── Validar receta: debe existir exactamente una fila con flor primaria ──
        var recetaFlorPrimaria = product.ProductRecipes
            .FirstOrDefault(pr => pr.InventoryItem.EsFlorPrimaria);

        if (recetaFlorPrimaria == null)
        {
            throw new AppException(
                "El producto no tiene una flor primaria definida en su receta. " +
                "No se puede calcular el limite de venta instantanea.");
        }

        // ── Calcular limite efectivo ──────────────────────────────────
        var limiteEfectivo = await CalcularLimiteEfectivoInternoAsync(
            product, recetaFlorPrimaria);

        // ── Rechazo automatico: cantidad excede limite ────────────────
        if (request.Cantidad > limiteEfectivo)
        {
            var rechazada = new SolicitudVentaInstantanea
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                ProductId = request.ProductId,
                Cantidad = request.Cantidad,
                Estado = "RECHAZADA",
                MotivoRechazo = "Lo sentimos, no contamos con disponibilidad suficiente para esta cantidad en este momento.",
                CreadaEn = DateTime.UtcNow,
                DecididaEn = DateTime.UtcNow,
            };
            _context.SolicitudesVentaInstantanea.Add(rechazada);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Solicitud {Id} rechazada: cantidad {Cantidad} excede limite efectivo {Limite} para producto {ProductId}",
                rechazada.Id, request.Cantidad, limiteEfectivo, request.ProductId);

            return MapToDto(rechazada, product.Nombre);
        }

        // ── Crear solicitud en PENDIENTE ──────────────────────────────
        var solicitud = new SolicitudVentaInstantanea
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            ProductId = request.ProductId,
            Cantidad = request.Cantidad,
            Estado = "PENDIENTE",
            CreadaEn = DateTime.UtcNow,
        };
        _context.SolicitudesVentaInstantanea.Add(solicitud);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Solicitud {Id} creada como PENDIENTE: producto {ProductId}, cantidad {Cantidad}",
            solicitud.Id, request.ProductId, request.Cantidad);

        // SignalR: notificar al grupo admin que hay una solicitud pendiente
        var dtoAdmin = await ObtenerAsync(solicitud.Id);
        await _realtime.SolicitudPendienteAsync(dtoAdmin);

        return MapToDto(solicitud, product.Nombre);
    }

    public async Task<InstantSaleResponseDto> ObtenerSolicitudAsync(
        Guid customerId,
        Guid solicitudId)
    {
        var solicitud = await _context.SolicitudesVentaInstantanea
            .Include(s => s.Product)
            .FirstOrDefaultAsync(s => s.Id == solicitudId && s.CustomerId == customerId);

        if (solicitud == null)
            throw new NotFoundException("Solicitud de venta instantanea", solicitudId);

        return MapToDto(solicitud, solicitud.Product.Nombre);
    }

    public async Task<int> CalcularLimiteEfectivoAsync(Guid productId)
    {
        var product = await _context.Products
            .Include(p => p.ProductRecipes)
                .ThenInclude(pr => pr.InventoryItem)
            .FirstOrDefaultAsync(p => p.Id == productId);

        if (product == null)
            throw new NotFoundException("Producto", productId);

        var recetaFlorPrimaria = product.ProductRecipes
            .FirstOrDefault(pr => pr.InventoryItem.EsFlorPrimaria);

        if (recetaFlorPrimaria == null)
            return 0;

        return await CalcularLimiteEfectivoInternoAsync(product, recetaFlorPrimaria);
    }

    // ── Calculo interno del limite efectivo ───────────────────────────
    // disponible = InventoryItem.StockActual
    //            - SUM(Cantidad * CantidadRequerida) de solicitudes ACEPTADAS
    //              con ReservaExpiraEn > ahora, para ese InventoryItemId
    // limiteEfectivo = MIN(LimiteVentaInstantanea ?? int.MaxValue,
    //                      disponible / CantidadRequerida)
    private async Task<int> CalcularLimiteEfectivoInternoAsync(
        Product product,
        ProductRecipe recetaFlorPrimaria)
    {
        var inventoryItemId = recetaFlorPrimaria.InventoryItemId;
        var cantidadRequerida = recetaFlorPrimaria.CantidadRequerida;
        var stockActual = recetaFlorPrimaria.InventoryItem.StockActual;
        var ahora = DateTime.UtcNow;

        // Sumar las unidades reservadas por solicitudes ACEPTADAS vigentes
        // que usen el mismo insumo (flor primaria) via su receta de producto.
        var reservado = await _context.SolicitudesVentaInstantanea
            .Where(s => s.Estado == "ACEPTADA"
                     && s.ReservaExpiraEn != null
                     && s.ReservaExpiraEn > ahora)
            .Join(
                _context.ProductRecipes
                    .Where(pr => pr.InventoryItemId == inventoryItemId),
                s => s.ProductId,
                pr => pr.ProductId,
                (s, pr) => new { s.Cantidad, pr.CantidadRequerida })
            .SumAsync(x => x.Cantidad * x.CantidadRequerida);

        var disponible = stockActual - reservado;

        if (disponible <= 0 || cantidadRequerida <= 0)
            return 0;

        var limitePorStock = disponible / cantidadRequerida;
        var limiteProducto = product.LimiteVentaInstantanea ?? int.MaxValue;

        return Math.Max(0, Math.Min(limiteProducto, limitePorStock));
    }

    // ── Bloque 3: Aceptar ───────────────────────────────────────────

    public async Task<SolicitudVentaInstantaneaDto> AceptarAsync(
        Guid solicitudId, Guid decisorUsuarioId)
    {
        await ValidarDecisorAsync(decisorUsuarioId);

        var ahora = _fechas.AhoraUtc();

        // Lock optimista: solo actualiza si Estado sigue siendo PENDIENTE.
        var affected = await _context.SolicitudesVentaInstantanea
            .Where(s => s.Id == solicitudId && s.Estado == "PENDIENTE")
            .ExecuteUpdateAsync(set => set
                .SetProperty(s => s.Estado, "ACEPTADA")
                .SetProperty(s => s.DecididaEn, ahora)
                .SetProperty(s => s.DecididaPorUsuarioId, decisorUsuarioId)
                .SetProperty(s => s.ReservaExpiraEn, ahora.Add(ReservaDuracion)));

        if (affected == 0)
        {
            var existe = await _context.SolicitudesVentaInstantanea
                .AnyAsync(s => s.Id == solicitudId);
            if (!existe)
                throw new NotFoundException("SolicitudVentaInstantanea", solicitudId);
            throw new ConflictException(
                "Esta solicitud ya fue decidida por otra persona o expiro.");
        }

        await _audit.RegistrarAsync(
            accion:    "SOLICITUD_ACEPTADA",
            entidad:   "SolicitudVentaInstantanea",
            entidadId: solicitudId.ToString(),
            usuarioId: decisorUsuarioId,
            detalles:  new { ReservaMinutos = ReservaDuracion.TotalMinutes });

        _logger.LogInformation(
            "Solicitud {SolicitudId} ACEPTADA por usuario {UsuarioId}. Reserva de {Min} min.",
            solicitudId, decisorUsuarioId, ReservaDuracion.TotalMinutes);

        var resultadoAceptada = await ObtenerAsync(solicitudId);
        await _realtime.SolicitudDecididaAsync(solicitudId, resultadoAceptada);
        return resultadoAceptada;
    }

    // ── Bloque 3: Rechazar ───────────────────────────────────────────

    public async Task<SolicitudVentaInstantaneaDto> RechazarAsync(
        Guid solicitudId, Guid decisorUsuarioId, string? motivoRechazo)
    {
        await ValidarDecisorAsync(decisorUsuarioId);

        var ahora = _fechas.AhoraUtc();

        var affected = await _context.SolicitudesVentaInstantanea
            .Where(s => s.Id == solicitudId && s.Estado == "PENDIENTE")
            .ExecuteUpdateAsync(set => set
                .SetProperty(s => s.Estado, "RECHAZADA")
                .SetProperty(s => s.DecididaEn, ahora)
                .SetProperty(s => s.DecididaPorUsuarioId, decisorUsuarioId)
                .SetProperty(s => s.MotivoRechazo, motivoRechazo));

        if (affected == 0)
        {
            var existe = await _context.SolicitudesVentaInstantanea
                .AnyAsync(s => s.Id == solicitudId);
            if (!existe)
                throw new NotFoundException("SolicitudVentaInstantanea", solicitudId);
            throw new ConflictException(
                "Esta solicitud ya fue decidida por otra persona o expiro.");
        }

        await _audit.RegistrarAsync(
            accion:    "SOLICITUD_RECHAZADA",
            entidad:   "SolicitudVentaInstantanea",
            entidadId: solicitudId.ToString(),
            usuarioId: decisorUsuarioId,
            detalles:  new { MotivoRechazo = motivoRechazo ?? "(sin motivo)" });

        _logger.LogInformation(
            "Solicitud {SolicitudId} RECHAZADA por usuario {UsuarioId}. Motivo: {Motivo}",
            solicitudId, decisorUsuarioId, motivoRechazo ?? "(sin motivo)");

        var resultadoRechazada = await ObtenerAsync(solicitudId);
        await _realtime.SolicitudDecididaAsync(solicitudId, resultadoRechazada);
        return resultadoRechazada;
    }

    // ── Bloque 3: Listado admin ──────────────────────────────────────

    public async Task<PagedResultDto<SolicitudVentaInstantaneaDto>> ListarAdminAsync(
        string? estado, int page, int size)
    {
        page = Math.Max(1, page);
        size = Math.Clamp(size, 1, 100);

        var query = _context.SolicitudesVentaInstantanea
            .Include(s => s.Customer)
            .Include(s => s.Product)
            .Include(s => s.DecididaPor)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(estado))
            query = query.Where(s => s.Estado == estado.ToUpperInvariant());

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(s => s.CreadaEn)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(s => new SolicitudVentaInstantaneaDto
            {
                Id                   = s.Id,
                CustomerId           = s.CustomerId,
                NombreCliente        = (s.Customer.Nombre + " " + (s.Customer.Apellido ?? "")).Trim(),
                TelefonoCliente      = s.Customer.Telefono,
                ProductId            = s.ProductId,
                NombreProducto       = s.Product.Nombre,
                ImagenProducto       = s.Product.ImagenUrl,
                Cantidad             = s.Cantidad,
                Estado               = s.Estado,
                CreadaEn             = s.CreadaEn,
                EscaladaAEmpleadoEn  = s.EscaladaAEmpleadoEn,
                DecididaEn           = s.DecididaEn,
                DecididaPorNombre    = s.DecididaPor != null
                    ? (s.DecididaPor.Nombre + " " + s.DecididaPor.Apellido).Trim()
                    : null,
                MotivoRechazo        = s.MotivoRechazo,
                MotivoExpiracion     = s.MotivoExpiracion,
                ReservaExpiraEn      = s.ReservaExpiraEn,
                OrderId              = s.OrderId,
            })
            .ToListAsync();

        return new PagedResultDto<SolicitudVentaInstantaneaDto>
        {
            Items        = items,
            Total        = total,
            Pagina       = page,
            TamanoPagina = size,
            TotalPaginas = (int)Math.Ceiling(total / (double)size),
        };
    }

    // ── Bloque 3: Obtener por id (admin) ─────────────────────────────

    public async Task<SolicitudVentaInstantaneaDto> ObtenerAsync(Guid solicitudId)
    {
        var s = await _context.SolicitudesVentaInstantanea
            .Include(x => x.Customer)
            .Include(x => x.Product)
            .Include(x => x.DecididaPor)
            .FirstOrDefaultAsync(x => x.Id == solicitudId)
            ?? throw new NotFoundException("SolicitudVentaInstantanea", solicitudId);

        return new SolicitudVentaInstantaneaDto
        {
            Id                   = s.Id,
            CustomerId           = s.CustomerId,
            NombreCliente        = $"{s.Customer.Nombre} {s.Customer.Apellido}".Trim(),
            TelefonoCliente      = s.Customer.Telefono,
            ProductId            = s.ProductId,
            NombreProducto       = s.Product.Nombre,
            ImagenProducto       = s.Product.ImagenUrl,
            Cantidad             = s.Cantidad,
            Estado               = s.Estado,
            CreadaEn             = s.CreadaEn,
            EscaladaAEmpleadoEn  = s.EscaladaAEmpleadoEn,
            DecididaEn           = s.DecididaEn,
            DecididaPorNombre    = s.DecididaPor != null
                ? $"{s.DecididaPor.Nombre} {s.DecididaPor.Apellido}".Trim()
                : null,
            MotivoRechazo        = s.MotivoRechazo,
            MotivoExpiracion     = s.MotivoExpiracion,
            ReservaExpiraEn      = s.ReservaExpiraEn,
            OrderId              = s.OrderId,
        };
    }

    // ── Helpers privados ─────────────────────────────────────────────

    /// <summary>
    /// Verifica que el usuario sea ADMIN o el empleado responsable de turno
    /// activo. Si no cumple ninguna condicion, lanza 403.
    /// </summary>
    private async Task ValidarDecisorAsync(Guid usuarioId)
    {
        var usuario = await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == usuarioId)
            ?? throw new NotFoundException("User", usuarioId);

        var esAdmin = usuario.UserRoles.Any(ur =>
            ur.Role.Nombre.Equals("ADMIN", StringComparison.OrdinalIgnoreCase));

        if (esAdmin) return;
        if (usuario.EsResponsableTurno) return;

        throw new ForbiddenException(
            "Solo el ADMIN o el empleado responsable de turno pueden decidir solicitudes.");
    }

    // ── Mapeo (Bloque 2: para la respuesta de creacion del cliente) ──

    private static InstantSaleResponseDto MapToDto(
        SolicitudVentaInstantanea s,
        string productoNombre) =>
        new()
        {
            Id = s.Id,
            ProductId = s.ProductId,
            ProductoNombre = productoNombre,
            Cantidad = s.Cantidad,
            Estado = s.Estado,
            MotivoRechazo = s.MotivoRechazo,
            CreadaEn = s.CreadaEn,
            ReservaExpiraEn = s.ReservaExpiraEn,
        };
}
