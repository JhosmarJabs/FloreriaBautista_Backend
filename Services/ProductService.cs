using Microsoft.EntityFrameworkCore;
using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Products;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Services.Interfaces;

namespace FloreriaBautista.Services;

public class ProductService : IProductService
{
    private readonly AppDbContext            _context;
    private readonly ILogger<ProductService> _logger;

    public ProductService(AppDbContext context, ILogger<ProductService> logger)
    {
        _context = context;
        _logger  = logger;
    }

    // ── Listar público ────────────────────────────────────────────
    public async Task<PagedResultDto<ProductSummaryDto>> ListarPublicosAsync(
        string? busqueda, string? categoria, string? coleccion, int page, int size)
    {
        var query = _context.Products
            .Include(p => p.ProductCategories).ThenInclude(pc => pc.Category)
            .Include(p => p.ProductCollections).ThenInclude(pc => pc.Collection)
            .Include(p => p.InventoryItem)
            .Where(p => p.Estado == "ACTIVO")
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(busqueda))
            query = query.Where(p => p.Nombre.Contains(busqueda) || p.Descripcion.Contains(busqueda));

        if (!string.IsNullOrWhiteSpace(categoria))
            query = query.Where(p => p.ProductCategories.Any(pc =>
                pc.Category.Nombre.ToLower() == categoria.ToLower()));

        if (!string.IsNullOrWhiteSpace(coleccion))
            query = query.Where(p => p.ProductCollections.Any(pc =>
                pc.Collection.Nombre.ToLower() == coleccion.ToLower()));

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(p => p.Nombre)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(p => new ProductSummaryDto
            {
                Id        = p.Id,
                Nombre    = p.Nombre,
                PrecioBase = p.PrecioBase,
                Tipo      = p.Tipo,
                Estado    = p.Estado,
                ImagenUrl = p.ImagenUrl,
                Stock     = p.InventoryItem != null ? p.InventoryItem.StockActual : null
            })
            .ToListAsync();

        return new PagedResultDto<ProductSummaryDto>
        {
            Items       = items,
            Total       = total,
            Pagina      = page,
            TamanoPagina = size,
            TotalPaginas = (int)Math.Ceiling(total / (double)size)
        };
    }

    // ── Detalle público ───────────────────────────────────────────
    public async Task<ProductResponseDto> ObtenerPublicoAsync(Guid id)
    {
        var p = await _context.Products
            .Include(p => p.ProductCategories).ThenInclude(pc => pc.Category)
            .Include(p => p.ProductCollections).ThenInclude(pc => pc.Collection)
            .Include(p => p.InventoryItem)
            .FirstOrDefaultAsync(p => p.Id == id && p.Estado == "ACTIVO")
            ?? throw new NotFoundException("Producto", id);

        return MapToDto(p);
    }

    // ── Listar admin ──────────────────────────────────────────────
    public async Task<PagedResultDto<ProductSummaryDto>> ListarAdminAsync(
        string? busqueda, string? estado, int page, int size)
    {
        var query = _context.Products
            .Include(p => p.InventoryItem)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(busqueda))
            query = query.Where(p => p.Nombre.Contains(busqueda));

        if (!string.IsNullOrWhiteSpace(estado))
            query = query.Where(p => p.Estado == estado.ToUpper());

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(p => p.CreadoEn)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(p => new ProductSummaryDto
            {
                Id        = p.Id,
                Nombre    = p.Nombre,
                PrecioBase = p.PrecioBase,
                Tipo      = p.Tipo,
                Estado    = p.Estado,
                ImagenUrl = p.ImagenUrl,
                Stock     = p.InventoryItem != null ? p.InventoryItem.StockActual : null
            })
            .ToListAsync();

        return new PagedResultDto<ProductSummaryDto>
        {
            Items        = items,
            Total        = total,
            Pagina       = page,
            TamanoPagina = size,
            TotalPaginas = (int)Math.Ceiling(total / (double)size)
        };
    }

    // ── Crear ─────────────────────────────────────────────────────
    public async Task<ProductResponseDto> CrearAsync(CreateProductRequestDto request)
    {
        var producto = new Product
        {
            Id               = Guid.NewGuid(),
            Nombre           = request.Nombre.Trim(),
            Descripcion      = request.Descripcion.Trim(),
            PrecioBase       = request.PrecioBase,
            Tipo             = request.Tipo.Trim().ToUpper(),
            EsPersonalizable = request.EsPersonalizable,
            Estado           = request.Estado.Trim().ToUpper(),
            ImagenUrl        = request.ImagenUrl,
            CreadoEn         = DateTime.UtcNow
        };

        await AsignarCategorias(producto, request.Categorias);
        await AsignarColecciones(producto, request.Colecciones);

        _context.Products.Add(producto);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Producto creado: {Nombre} ({Id})", producto.Nombre, producto.Id);

        return await ObtenerConDetalleAsync(producto.Id);
    }

    // ── Actualizar ────────────────────────────────────────────────
    public async Task<ProductResponseDto> ActualizarAsync(Guid id, UpdateProductRequestDto request)
    {
        var producto = await _context.Products
            .Include(p => p.ProductCategories)
            .Include(p => p.ProductCollections)
            .FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new NotFoundException("Producto", id);

        if (!string.IsNullOrWhiteSpace(request.Nombre))      producto.Nombre      = request.Nombre.Trim();
        if (!string.IsNullOrWhiteSpace(request.Descripcion)) producto.Descripcion = request.Descripcion.Trim();
        if (request.PrecioBase.HasValue)                     producto.PrecioBase  = request.PrecioBase.Value;
        if (!string.IsNullOrWhiteSpace(request.Tipo))        producto.Tipo        = request.Tipo.Trim().ToUpper();
        if (request.EsPersonalizable.HasValue)               producto.EsPersonalizable = request.EsPersonalizable.Value;
        if (!string.IsNullOrWhiteSpace(request.Estado))      producto.Estado      = request.Estado.Trim().ToUpper();
        if (request.ImagenUrl != null)                       producto.ImagenUrl   = request.ImagenUrl;

        if (request.Categorias != null)
        {
            _context.RemoveRange(producto.ProductCategories);
            await AsignarCategorias(producto, request.Categorias);
        }

        if (request.Colecciones != null)
        {
            _context.RemoveRange(producto.ProductCollections);
            await AsignarColecciones(producto, request.Colecciones);
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Producto actualizado: {Id}", id);
        return await ObtenerConDetalleAsync(id);
    }

    // ── Helpers ───────────────────────────────────────────────────
    private async Task AsignarCategorias(Product producto, List<string> nombres)
    {
        foreach (var nombre in nombres)
        {
            var cat = await _context.Categories
                .FirstOrDefaultAsync(c => c.Nombre.ToLower() == nombre.ToLower().Trim());
            if (cat != null)
                producto.ProductCategories.Add(new ProductCategory { ProductId = producto.Id, CategoryId = cat.Id });
        }
    }

    private async Task AsignarColecciones(Product producto, List<string> nombres)
    {
        foreach (var nombre in nombres)
        {
            var col = await _context.Collections
                .FirstOrDefaultAsync(c => c.Nombre.ToLower() == nombre.ToLower().Trim());
            if (col != null)
                producto.ProductCollections.Add(new ProductCollection { ProductId = producto.Id, CollectionId = col.Id });
        }
    }

    private async Task<ProductResponseDto> ObtenerConDetalleAsync(Guid id)
    {
        var p = await _context.Products
            .Include(p => p.ProductCategories).ThenInclude(pc => pc.Category)
            .Include(p => p.ProductCollections).ThenInclude(pc => pc.Collection)
            .Include(p => p.InventoryItem)
            .FirstAsync(p => p.Id == id);
        return MapToDto(p);
    }

    private static ProductResponseDto MapToDto(Product p) => new()
    {
        Id               = p.Id,
        Nombre           = p.Nombre,
        Descripcion      = p.Descripcion,
        PrecioBase       = p.PrecioBase,
        Tipo             = p.Tipo,
        EsPersonalizable = p.EsPersonalizable,
        Estado           = p.Estado,
        ImagenUrl        = p.ImagenUrl,
        Categorias       = p.ProductCategories.Select(pc => pc.Category.Nombre).ToList(),
        Colecciones      = p.ProductCollections.Select(pc => pc.Collection.Nombre).ToList(),
        StockActual      = p.InventoryItem?.StockActual,
        CreadoEn         = p.CreadoEn
    };
}
