using Microsoft.EntityFrameworkCore;
using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Inventory;
using FloreriaBautista.Models.DTOs.SupplyOrders;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Services.Interfaces;

namespace FloreriaBautista.Services;

/// <summary>
/// Solicitudes de reabastecimiento: el documento que se manda al proveedor y contra el
/// que después se confirma, línea por línea, qué llegó. La confirmación es la que da de
/// alta las entradas al inventario, para no recapturar insumo por insumo a mano.
/// </summary>
public class SupplyOrderService : ISupplyOrderService
{
    private readonly AppDbContext                _context;
    private readonly IInventoryService           _inventoryService;
    private readonly IAuditService               _audit;
    private readonly ILogger<SupplyOrderService> _logger;

    private static readonly string[] EstadosValidos =
        ["BORRADOR", "ENVIADA", "RECIBIDA_PARCIAL", "RECIBIDA", "CANCELADA"];

    public SupplyOrderService(AppDbContext context, IInventoryService inventoryService,
                              IAuditService audit, ILogger<SupplyOrderService> logger)
    {
        _context          = context;
        _inventoryService = inventoryService;
        _audit            = audit;
        _logger           = logger;
    }

    // ── Listar ────────────────────────────────────────────────────
    public async Task<PagedResultDto<SupplyOrderListItemDto>> ListarAsync(
        string? estado, DateTime? desde, DateTime? hasta, int page, int size)
    {
        if (page <= 0) page = 1;
        if (size <= 0) size = 20;
        if (size > 100) size = 100;

        var query = _context.SupplyOrders.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(estado))
        {
            var e = estado.Trim().ToUpper();
            if (!EstadosValidos.Contains(e))
                throw new AppException($"Estado inválido. Use: {string.Join(", ", EstadosValidos)}.");
            query = query.Where(o => o.Estado == e);
        }

        if (desde.HasValue)
            query = query.Where(o => o.FechaSolicitud >= DateTime.SpecifyKind(desde.Value.Date, DateTimeKind.Utc));

        if (hasta.HasValue)
        {
            // 'hasta' es inclusivo: se compara contra el final del día.
            var fin = DateTime.SpecifyKind(hasta.Value.Date.AddDays(1), DateTimeKind.Utc);
            query = query.Where(o => o.FechaSolicitud < fin);
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(o => o.FechaSolicitud)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(o => new SupplyOrderListItemDto
            {
                Id                 = o.Id,
                Folio              = o.Folio,
                Estado             = o.Estado,
                Proveedor          = o.Proveedor,
                FechaSolicitud     = o.FechaSolicitud,
                FechaEnvio         = o.FechaEnvio,
                FechaRecepcion     = o.FechaRecepcion,
                SemanaObjetivo     = o.SemanaObjetivo,
                TotalLineas        = o.Items.Count,
                LineasConfirmadas  = o.Items.Count(i => i.CantidadRecibida != null),
                PorcentajeRecibido = o.Items.Count == 0
                    ? 0
                    : o.Items.Count(i => i.CantidadRecibida != null) * 100 / o.Items.Count,
                TotalEstimado      = o.TotalEstimado
            })
            .ToListAsync();

        return new PagedResultDto<SupplyOrderListItemDto>
        {
            Items        = items,
            Total        = total,
            Pagina       = page,
            TamanoPagina = size,
            TotalPaginas = (int)Math.Ceiling(total / (double)size),
            SumaTotal    = await query.SumAsync(o => (decimal?)o.TotalEstimado) ?? 0
        };
    }

    // ── Detalle ───────────────────────────────────────────────────
    public async Task<SupplyOrderDetailDto> ObtenerAsync(Guid id)
    {
        var solicitud = await _context.SupplyOrders
            .AsNoTracking()
            .Include(o => o.Usuario)
            .Include(o => o.Items.OrderBy(i => i.NombreSnapshot))
            .FirstOrDefaultAsync(o => o.Id == id)
            ?? throw new NotFoundException("SupplyOrder", id);

        return MapearDetalle(solicitud);
    }

    // ── Crear ─────────────────────────────────────────────────────
    public async Task<SupplyOrderDetailDto> CrearAsync(CreateSupplyOrderDto request, Guid usuarioId)
    {
        var lineas = NormalizarLineas(request.Lineas);
        var insumos = await CargarInsumosAsync(lineas.Select(l => l.InventoryItemId).ToList());

        var solicitud = new SupplyOrder
        {
            Id             = Guid.NewGuid(),
            Folio          = await GenerarFolioAsync(),
            Estado         = "BORRADOR",
            Proveedor      = Limpiar(request.Proveedor),
            FechaSolicitud = DateTime.UtcNow,
            SemanaObjetivo = Limpiar(request.SemanaObjetivo),
            Notas          = Limpiar(request.Notas),
            UsuarioId      = usuarioId
        };

        solicitud.Items = lineas.Select(l => ConstruirLinea(solicitud.Id, l, insumos[l.InventoryItemId])).ToList();
        solicitud.TotalEstimado = solicitud.Items.Sum(i => i.CantidadSolicitada * (i.PrecioUnitario ?? 0));

        _context.SupplyOrders.Add(solicitud);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Solicitud de reabastecimiento {Folio} creada con {Lineas} línea(s)",
            solicitud.Folio, solicitud.Items.Count);

        await _audit.RegistrarAsync("CREAR_SOLICITUD_REABASTECIMIENTO", "SupplyOrder", solicitud.Id.ToString(), usuarioId,
            new { solicitud.Folio, Lineas = solicitud.Items.Count, solicitud.TotalEstimado });

        return await ObtenerAsync(solicitud.Id);
    }

    // ── Editar (solo en BORRADOR) ─────────────────────────────────
    public async Task<SupplyOrderDetailDto> ActualizarAsync(Guid id, UpdateSupplyOrderDto request, Guid usuarioId)
    {
        var solicitud = await _context.SupplyOrders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id)
            ?? throw new NotFoundException("SupplyOrder", id);

        if (solicitud.Estado != "BORRADOR")
            throw new AppException(
                $"La solicitud {solicitud.Folio} está {solicitud.Estado}: solo se puede editar en BORRADOR.");

        var lineas  = NormalizarLineas(request.Lineas);
        var insumos = await CargarInsumosAsync(lineas.Select(l => l.InventoryItemId).ToList());

        solicitud.Proveedor      = Limpiar(request.Proveedor);
        solicitud.SemanaObjetivo = Limpiar(request.SemanaObjetivo);
        solicitud.Notas          = Limpiar(request.Notas);

        _context.SupplyOrderItems.RemoveRange(solicitud.Items);
        solicitud.Items = lineas.Select(l => ConstruirLinea(solicitud.Id, l, insumos[l.InventoryItemId])).ToList();
        solicitud.TotalEstimado = solicitud.Items.Sum(i => i.CantidadSolicitada * (i.PrecioUnitario ?? 0));

        await _context.SaveChangesAsync();

        await _audit.RegistrarAsync("EDITAR_SOLICITUD_REABASTECIMIENTO", "SupplyOrder", solicitud.Id.ToString(), usuarioId,
            new { solicitud.Folio, Lineas = solicitud.Items.Count, solicitud.TotalEstimado });

        return await ObtenerAsync(solicitud.Id);
    }

    // ── Enviar ────────────────────────────────────────────────────
    public async Task<SupplyOrderDetailDto> EnviarAsync(Guid id, Guid usuarioId)
    {
        var solicitud = await _context.SupplyOrders.FirstOrDefaultAsync(o => o.Id == id)
            ?? throw new NotFoundException("SupplyOrder", id);

        if (solicitud.Estado != "BORRADOR")
            throw new AppException(
                $"La solicitud {solicitud.Folio} ya está {solicitud.Estado}: solo se envía desde BORRADOR.");

        solicitud.Estado     = "ENVIADA";
        solicitud.FechaEnvio = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _audit.RegistrarAsync("ENVIAR_SOLICITUD_REABASTECIMIENTO", "SupplyOrder", solicitud.Id.ToString(), usuarioId,
            new { solicitud.Folio, solicitud.Proveedor });

        return await ObtenerAsync(solicitud.Id);
    }

    // ── Recepción ─────────────────────────────────────────────────
    // El núcleo: confirmar qué llegó y dar de alta las entradas al inventario en una
    // sola transacción, reutilizando InventoryService para no duplicar la aritmética
    // de stock ni escribir StockActual a mano desde aquí.
    public async Task<SupplyOrderDetailDto> RegistrarRecepcionAsync(
        Guid id, ReceiveSupplyOrderDto request, Guid usuarioId)
    {
        var solicitud = await _context.SupplyOrders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id)
            ?? throw new NotFoundException("SupplyOrder", id);

        if (solicitud.Estado == "BORRADOR")
            throw new AppException(
                $"La solicitud {solicitud.Folio} sigue en BORRADOR: envíala al proveedor antes de recibirla.");

        if (solicitud.Estado is "CANCELADA" or "RECIBIDA" or "RECIBIDA_PARCIAL")
            throw new AppException(
                $"La solicitud {solicitud.Folio} ya está {solicitud.Estado} y no admite más recepciones.");

        if (request.Lineas.Count == 0)
            throw new AppException("No se recibió ninguna línea para confirmar.");

        var duplicadas = request.Lineas.GroupBy(l => l.ItemId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicadas.Count > 0)
            throw new AppException($"La misma línea llegó repetida en la recepción: {string.Join(", ", duplicadas)}.");

        var ahora       = DateTime.UtcNow;
        var movimientos = new List<InventoryMovementDto>();
        var omitidas    = 0;

        await using var tx = await _context.Database.BeginTransactionAsync();

        foreach (var confirmacion in request.Lineas)
        {
            var linea = solicitud.Items.FirstOrDefault(i => i.Id == confirmacion.ItemId)
                ?? throw new AppException(
                    $"La línea {confirmacion.ItemId} no pertenece a la solicitud {solicitud.Folio}.");

            // Idempotencia: si la línea ya generó su movimiento de entrada, reenviar el POST
            // no vuelve a sumar stock. Las líneas que no llegaron (sin movimiento) sí pueden
            // volver a confirmarse en una visita posterior del proveedor.
            if (linea.InventoryMovementId != null)
            {
                omitidas++;
                continue;
            }

            if (confirmacion.CantidadRecibida > 0)
            {
                var movimiento = await _inventoryService.RegistrarMovimientoAsync(new RegisterMovementRequestDto
                {
                    InventoryItemId = linea.InventoryItemId,
                    Tipo            = "ENTRADA",
                    Cantidad        = confirmacion.CantidadRecibida,
                    Motivo          = $"Recepción {solicitud.Folio}"
                }, usuarioId);

                linea.InventoryMovementId = movimiento.Id;
                movimientos.Add(movimiento);
            }

            linea.CantidadRecibida = confirmacion.CantidadRecibida;
            linea.EstadoLinea      = CalcularEstadoLinea(linea.CantidadSolicitada, confirmacion.CantidadRecibida);
            linea.RecibidoEn       = ahora;

            if (confirmacion.PrecioUnitario is > 0)          linea.PrecioUnitario = confirmacion.PrecioUnitario;
            if (!string.IsNullOrWhiteSpace(confirmacion.Observacion)) linea.Observacion = Limpiar(confirmacion.Observacion);
        }

        var todasSurtidas = solicitud.Items.All(i => i.EstadoLinea is "COMPLETO" or "EXCEDENTE");

        if (todasSurtidas)
        {
            solicitud.Estado         = "RECIBIDA";
            solicitud.FechaRecepcion = ahora;
        }
        else if (request.CerrarSolicitud)
        {
            solicitud.Estado         = "RECIBIDA_PARCIAL";
            solicitud.FechaRecepcion = ahora;
        }
        // Si no se cierra, se queda ENVIADA y admite otra recepción más adelante.

        await _context.SaveChangesAsync();
        await tx.CommitAsync();

        _logger.LogInformation(
            "Recepción de {Folio}: {Movimientos} entrada(s) de inventario, {Omitidas} línea(s) ya confirmadas, estado {Estado}",
            solicitud.Folio, movimientos.Count, omitidas, solicitud.Estado);

        // Fuera de la transacción: la auditoría nunca debe tumbar la operación principal.
        await _audit.RegistrarAsync("RECEPCION_SOLICITUD_REABASTECIMIENTO", "SupplyOrder", solicitud.Id.ToString(), usuarioId,
            new
            {
                solicitud.Folio,
                EstadoFinal      = solicitud.Estado,
                request.CerrarSolicitud,
                LineasOmitidas   = omitidas,
                Entradas         = movimientos.Select(m => new
                {
                    m.Id, m.InventoryItemId, m.NombreItem, m.Cantidad, m.StockAntes, m.StockDespues
                })
            });

        return await ObtenerAsync(solicitud.Id);
    }

    // ── Cancelar ──────────────────────────────────────────────────
    public async Task<SupplyOrderDetailDto> CancelarAsync(Guid id, CancelSupplyOrderDto request, Guid usuarioId)
    {
        var solicitud = await _context.SupplyOrders.FirstOrDefaultAsync(o => o.Id == id)
            ?? throw new NotFoundException("SupplyOrder", id);

        if (solicitud.Estado is "CANCELADA")
            throw new AppException($"La solicitud {solicitud.Folio} ya estaba cancelada.");

        if (solicitud.Estado is "RECIBIDA" or "RECIBIDA_PARCIAL")
            throw new AppException(
                $"La solicitud {solicitud.Folio} ya tiene mercancía recibida: no se puede cancelar.");

        var motivo = Limpiar(request.Motivo);
        solicitud.Estado = "CANCELADA";
        solicitud.Notas  = string.IsNullOrWhiteSpace(motivo)
            ? solicitud.Notas
            : Recortar(string.IsNullOrWhiteSpace(solicitud.Notas)
                ? $"Cancelada: {motivo}"
                : $"{solicitud.Notas}\nCancelada: {motivo}", 500);

        await _context.SaveChangesAsync();

        await _audit.RegistrarAsync("CANCELAR_SOLICITUD_REABASTECIMIENTO", "SupplyOrder", solicitud.Id.ToString(), usuarioId,
            new { solicitud.Folio, Motivo = motivo });

        return await ObtenerAsync(solicitud.Id);
    }

    // ── Helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Folio legible y secuencial por año: REAB-2026-0007. Se calcula sobre el último
    /// folio del año en curso; el índice único sobre folio es la red de seguridad si
    /// dos solicitudes se crearan en el mismo instante.
    /// </summary>
    private async Task<string> GenerarFolioAsync()
    {
        var prefijo = $"REAB-{DateTime.UtcNow.Year}-";

        var ultimo = await _context.SupplyOrders
            .Where(o => o.Folio.StartsWith(prefijo))
            .OrderByDescending(o => o.Folio)
            .Select(o => o.Folio)
            .FirstOrDefaultAsync();

        var consecutivo = 1;
        if (ultimo is not null && int.TryParse(ultimo[prefijo.Length..], out var n))
            consecutivo = n + 1;

        return prefijo + consecutivo.ToString("D4");
    }

    /// <summary>Une líneas repetidas del mismo insumo en una sola (gana la última cantidad).</summary>
    private static List<SupplyOrderLineInputDto> NormalizarLineas(List<SupplyOrderLineInputDto> lineas)
    {
        if (lineas is null || lineas.Count == 0)
            throw new AppException("La solicitud debe llevar al menos un insumo.");

        var normalizadas = lineas
            .Where(l => l.Cantidad > 0)
            .GroupBy(l => l.InventoryItemId)
            .Select(g => g.Last())
            .ToList();

        if (normalizadas.Count == 0)
            throw new AppException("La solicitud debe llevar al menos un insumo con cantidad mayor a cero.");

        return normalizadas;
    }

    private async Task<Dictionary<Guid, InventoryItem>> CargarInsumosAsync(List<Guid> ids)
    {
        var insumos = await _context.InventoryItems
            .Where(i => ids.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id);

        var faltantes = ids.Where(id => !insumos.ContainsKey(id)).ToList();
        if (faltantes.Count > 0)
            throw new NotFoundException("InventoryItem", faltantes[0]);

        return insumos;
    }

    /// <summary>Congela nombre, unidad y costo del insumo al momento de solicitar.</summary>
    private static SupplyOrderItem ConstruirLinea(Guid solicitudId, SupplyOrderLineInputDto input, InventoryItem insumo) =>
        new()
        {
            Id                 = Guid.NewGuid(),
            SupplyOrderId      = solicitudId,
            InventoryItemId    = insumo.Id,
            NombreSnapshot     = insumo.Nombre,
            UnidadMedida       = insumo.UnidadMedida,
            CantidadSolicitada = input.Cantidad,
            EstadoLinea        = "PENDIENTE",
            PrecioUnitario     = insumo.PrecioCosto,
            Origen             = Recortar(Limpiar(input.Origen) ?? "Manual", 120)!
        };

    private static string CalcularEstadoLinea(int solicitada, int recibida) => recibida switch
    {
        0                     => "NO_LLEGO",
        _ when recibida <  solicitada => "PARCIAL",
        _ when recibida == solicitada => "COMPLETO",
        _                     => "EXCEDENTE"
    };

    private static SupplyOrderDetailDto MapearDetalle(SupplyOrder o)
    {
        var confirmadas = o.Items.Count(i => i.CantidadRecibida != null);

        return new SupplyOrderDetailDto
        {
            Id                 = o.Id,
            Folio              = o.Folio,
            Estado             = o.Estado,
            Proveedor          = o.Proveedor,
            FechaSolicitud     = o.FechaSolicitud,
            FechaEnvio         = o.FechaEnvio,
            FechaRecepcion     = o.FechaRecepcion,
            SemanaObjetivo     = o.SemanaObjetivo,
            Notas              = o.Notas,
            UsuarioId          = o.UsuarioId,
            UsuarioNombre      = o.Usuario is null ? null : $"{o.Usuario.Nombre} {o.Usuario.Apellido}".Trim(),
            TotalLineas        = o.Items.Count,
            LineasConfirmadas  = confirmadas,
            PorcentajeRecibido = o.Items.Count == 0 ? 0 : confirmadas * 100 / o.Items.Count,
            TotalEstimado      = o.TotalEstimado,
            Lineas             = o.Items.Select(i => new SupplyOrderLineDto
            {
                Id                  = i.Id,
                InventoryItemId     = i.InventoryItemId,
                NombreSnapshot      = i.NombreSnapshot,
                UnidadMedida        = i.UnidadMedida,
                CantidadSolicitada  = i.CantidadSolicitada,
                CantidadRecibida    = i.CantidadRecibida,
                EstadoLinea         = i.EstadoLinea,
                PrecioUnitario      = i.PrecioUnitario,
                Origen              = i.Origen,
                Observacion         = i.Observacion,
                RecibidoEn          = i.RecibidoEn,
                InventoryMovementId = i.InventoryMovementId
            }).ToList()
        };
    }

    private static string? Limpiar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static string? Recortar(string? valor, int max) =>
        valor is null || valor.Length <= max ? valor : valor[..max];
}
