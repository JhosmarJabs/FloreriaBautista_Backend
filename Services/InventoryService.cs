using Microsoft.EntityFrameworkCore;
using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Inventory;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Services.Interfaces;

namespace FloreriaBautista.Services;

public class InventoryService : IInventoryService
{
    private readonly AppDbContext              _context;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(AppDbContext context, ILogger<InventoryService> logger)
    {
        _context = context;
        _logger  = logger;
    }

    // ── Listar ────────────────────────────────────────────────────
    public async Task<PagedResultDto<InventoryItemDto>> ListarAsync(
        string? sucursal, bool? bajoMinimo, string? busqueda, int page, int size)
    {
        var query = _context.InventoryItems
            .Where(i => i.Activo)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(sucursal))
            query = query.Where(i => i.Sucursal.ToLower() == sucursal.ToLower());

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var b = busqueda.ToLower().Trim();
            query = query.Where(i => i.Nombre.ToLower().Contains(b));
        }

        var items = await query.OrderBy(i => i.Nombre).ToListAsync();

        if (bajoMinimo == true)
            items = items.Where(i => i.StockActual <= i.StockMinimo).ToList();

        var total    = items.Count;
        var paginado = items.Skip((page - 1) * size).Take(size).Select(MapToDto).ToList();

        return new PagedResultDto<InventoryItemDto>
        {
            Items        = paginado,
            Total        = total,
            Pagina       = page,
            TamanoPagina = size,
            TotalPaginas = (int)Math.Ceiling(total / (double)size)
        };
    }

    // ── Detalle ───────────────────��──────────────────────────────��
    public async Task<InventoryItemDto> ObtenerAsync(Guid id)
    {
        var item = await _context.InventoryItems.FindAsync(id)
            ?? throw new NotFoundException("InventoryItem", id);
        return MapToDto(item);
    }

    // ── Crear ─────────────────────────��───────────────────────────
    public async Task<InventoryItemDto> CrearAsync(CreateInventoryItemDto request)
    {
        var item = new InventoryItem
        {
            Id           = Guid.NewGuid(),
            Nombre       = request.Nombre.Trim(),
            StockActual  = request.StockActual,
            StockMinimo  = request.StockMinimo,
            Sucursal     = request.Sucursal.Trim().ToUpper(),
            SumaAlCosto  = request.SumaAlCosto,
            UnidadMedida = request.UnidadMedida?.Trim().ToUpper(),
            PrecioCosto  = request.PrecioCosto,
            EsFlorPrimaria = request.EsFlorPrimaria,
            ImagenUrl    = request.ImagenUrl?.Trim(),
            Activo       = true
        };

        _context.InventoryItems.Add(item);
        await _context.SaveChangesAsync();
        _logger.LogInformation("InventoryItem creado: {Nombre} ({Id})", item.Nombre, item.Id);
        return MapToDto(item);
    }

    // ── Actualizar ────────────────────────────────────────────────
    public async Task<InventoryItemDto> ActualizarAsync(Guid id, UpdateInventoryItemDto request)
    {
        var item = await _context.InventoryItems.FindAsync(id)
            ?? throw new NotFoundException("InventoryItem", id);

        if (!string.IsNullOrWhiteSpace(request.Nombre))      item.Nombre      = request.Nombre.Trim();
        if (request.StockActual.HasValue)                     item.StockActual = request.StockActual.Value;
        if (request.StockMinimo.HasValue)                     item.StockMinimo = request.StockMinimo.Value;
        if (request.PrecioCosto.HasValue)                     item.PrecioCosto = request.PrecioCosto.Value;
        if (request.EsFlorPrimaria.HasValue)                 item.EsFlorPrimaria = request.EsFlorPrimaria.Value;
        if (!string.IsNullOrWhiteSpace(request.Sucursal))    item.Sucursal    = request.Sucursal.Trim().ToUpper();
        if (request.SumaAlCosto.HasValue)                     item.SumaAlCosto = request.SumaAlCosto.Value;
        
        if (request.UnidadMedida != null)
            item.UnidadMedida = string.IsNullOrWhiteSpace(request.UnidadMedida)
                ? null : request.UnidadMedida.Trim().ToUpper();

        if (request.ImagenUrl != null)
            item.ImagenUrl = string.IsNullOrWhiteSpace(request.ImagenUrl)
                ? null : request.ImagenUrl.Trim();

        if (request.Activo.HasValue) item.Activo = request.Activo.Value;

        await _context.SaveChangesAsync();
        _logger.LogInformation("InventoryItem actualizado: {Id}", id);
        return MapToDto(item);
    }

    public async Task EliminarAsync(Guid id)
    {
        var item = await _context.InventoryItems.FindAsync(id)
            ?? throw new NotFoundException("InventoryItem", id);

        item.Activo = false;
        await _context.SaveChangesAsync();
        _logger.LogInformation("InventoryItem desactivado (borrado lógico): {Id}", id);
    }

    // ── Registrar movimiento ──────────────────────────────────────
    public async Task<InventoryMovementDto> RegistrarMovimientoAsync(
        RegisterMovementRequestDto request, Guid usuarioId)
    {
        var item = await _context.InventoryItems.FindAsync(request.InventoryItemId)
            ?? throw new NotFoundException("InventoryItem", request.InventoryItemId);

        var tipo = request.Tipo.ToUpper();
        if (tipo != "ENTRADA" && tipo != "SALIDA" && tipo != "AJUSTE")
            throw new AppException("Tipo de movimiento inválido. Use: ENTRADA, SALIDA o AJUSTE.");

        var stockAntes = item.StockActual;

        item.StockActual = tipo switch
        {
            "ENTRADA" => item.StockActual + request.Cantidad,
            "SALIDA"  => item.StockActual - request.Cantidad,
            "AJUSTE"  => request.Cantidad,
            _         => item.StockActual
        };

        if (item.StockActual < 0)
            throw new AppException(
                $"Stock insuficiente. Stock actual: {stockAntes}, se intenta retirar: {request.Cantidad}");

        var movimiento = new InventoryMovement
        {
            Id              = Guid.NewGuid(),
            InventoryItemId = item.Id,
            TipoMovimiento  = tipo,
            Cantidad        = request.Cantidad,
            Motivo          = request.Motivo,
            UsuarioId       = usuarioId,
            FechaHora       = DateTime.UtcNow
        };

        _context.InventoryMovements.Add(movimiento);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Movimiento {Tipo} en item {Id}: {Antes}→{Despues}",
            tipo, item.Id, stockAntes, item.StockActual);

        return new InventoryMovementDto
        {
            Id              = movimiento.Id,
            InventoryItemId = item.Id,
            NombreItem      = item.Nombre,
            Tipo            = tipo,
            Cantidad        = request.Cantidad,
            StockAntes      = stockAntes,
            StockDespues    = item.StockActual,
            Motivo          = request.Motivo,
            FechaHora       = movimiento.FechaHora
        };
    }

    // ── Listar movimientos ─────────────────────��──────────────────
    public async Task<PagedResultDto<InventoryMovementDto>> ListarMovimientosAsync(
        Guid? inventoryItemId, int page, int size)
    {
        var query = _context.InventoryMovements
            .Include(m => m.InventoryItem)
            .AsQueryable();

        if (inventoryItemId.HasValue)
            query = query.Where(m => m.InventoryItemId == inventoryItemId.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(m => m.FechaHora)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(m => new InventoryMovementDto
            {
                Id              = m.Id,
                InventoryItemId = m.InventoryItemId,
                NombreItem      = m.InventoryItem.Nombre,
                Tipo            = m.TipoMovimiento,
                Cantidad        = m.Cantidad,
                Motivo          = m.Motivo,
                FechaHora       = m.FechaHora
            })
            .ToListAsync();

        return new PagedResultDto<InventoryMovementDto>
        {
            Items        = items,
            Total        = total,
            Pagina       = page,
            TamanoPagina = size,
            TotalPaginas = (int)Math.Ceiling(total / (double)size)
        };
    }

    private static InventoryItemDto MapToDto(InventoryItem i) => new()
    {
        Id           = i.Id,
        Nombre       = i.Nombre,
        StockActual  = i.StockActual,
        StockMinimo  = i.StockMinimo,
        Sucursal     = i.Sucursal,
        SumaAlCosto  = i.SumaAlCosto,
        UnidadMedida = i.UnidadMedida,
        PrecioCosto  = i.PrecioCosto,
        EsFlorPrimaria = i.EsFlorPrimaria,
        ImagenUrl    = i.ImagenUrl,
        Activo       = i.Activo
    };
}
