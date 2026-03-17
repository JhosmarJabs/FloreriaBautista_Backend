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

    // ── Listar inventario ─────────────────────────────────────────
    public async Task<PagedResultDto<InventoryItemDto>> ListarAsync(
        string? sucursal, bool? bajoMinimo, int page, int size)
    {
        var query = _context.InventoryItems
            .Include(i => i.Product)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(sucursal))
            query = query.Where(i => i.Sucursal.ToLower() == sucursal.ToLower());

        var items = await query.OrderBy(i => i.Product.Nombre).ToListAsync();

        // Filtro bajoMinimo en memoria (no se puede en SQL con propiedad calculada)
        if (bajoMinimo == true)
            items = items.Where(i => i.StockActual <= i.StockMinimo).ToList();

        var total    = items.Count;
        var paginado = items.Skip((page - 1) * size).Take(size)
            .Select(MapToDto).ToList();

        return new PagedResultDto<InventoryItemDto>
        {
            Items        = paginado,
            Total        = total,
            Pagina       = page,
            TamanoPagina = size,
            TotalPaginas = (int)Math.Ceiling(total / (double)size)
        };
    }

    // ── Detalle por producto ──────────────────────────────────────
    public async Task<InventoryItemDto> ObtenerPorProductoAsync(Guid productId)
    {
        var item = await _context.InventoryItems
            .Include(i => i.Product)
            .FirstOrDefaultAsync(i => i.ProductId == productId)
            ?? throw new NotFoundException("InventoryItem para producto", productId);

        return MapToDto(item);
    }

    // ── Registrar movimiento ──────────────────────────────────────
    public async Task<InventoryMovementDto> RegistrarMovimientoAsync(
        RegisterMovementRequestDto request, Guid usuarioId)
    {
        var item = await _context.InventoryItems
            .Include(i => i.Product)
            .FirstOrDefaultAsync(i => i.ProductId == request.ProductId)
            ?? throw new NotFoundException("InventoryItem para producto", request.ProductId);

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
            throw new AppException($"Stock insuficiente. Stock actual: {stockAntes}, se intenta retirar: {request.Cantidad}");

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

        _logger.LogInformation("Movimiento {Tipo} en producto {Id}: {Antes}→{Despues}",
            tipo, request.ProductId, stockAntes, item.StockActual);

        return new InventoryMovementDto
        {
            Id           = movimiento.Id,
            ProductId    = item.ProductId,
            Producto     = item.Product.Nombre,
            Tipo         = tipo,
            Cantidad     = request.Cantidad,
            StockAntes   = stockAntes,
            StockDespues = item.StockActual,
            Motivo       = request.Motivo,
            FechaHora    = movimiento.FechaHora
        };
    }

    // ── Listar movimientos ────────────────────────────────────────
    public async Task<PagedResultDto<InventoryMovementDto>> ListarMovimientosAsync(
        Guid? productId, int page, int size)
    {
        var query = _context.InventoryMovements
            .Include(m => m.InventoryItem).ThenInclude(i => i.Product)
            .AsQueryable();

        if (productId.HasValue)
            query = query.Where(m => m.InventoryItem.ProductId == productId.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(m => m.FechaHora)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(m => new InventoryMovementDto
            {
                Id           = m.Id,
                ProductId    = m.InventoryItem.ProductId,
                Producto     = m.InventoryItem.Product.Nombre,
                Tipo         = m.TipoMovimiento,
                Cantidad     = m.Cantidad,
                Motivo       = m.Motivo,
                FechaHora    = m.FechaHora
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
        Id          = i.Id,
        ProductId   = i.ProductId,
        Nombre      = i.Product.Nombre,
        StockActual = i.StockActual,
        StockMinimo = i.StockMinimo,
        Sucursal    = i.Sucursal
    };
}
