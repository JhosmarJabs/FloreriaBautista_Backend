using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Products;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Services;

public class ProductService : IProductService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ProductService> _logger;

    public ProductService(AppDbContext context, ILogger<ProductService> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ── Listar público ────────────────────────────────────────────
    public async Task<PagedResultDto<ProductSummaryDto>> ListarPublicosAsync(
        string? busqueda,
        string? categoria,
        string? coleccion,
        int page,
        int size
    )
    {
        var query = _context
            .Products.Include(p => p.ProductCategories)
            .ThenInclude(pc => pc.Category)
            .Include(p => p.ProductCollections)
            .ThenInclude(pc => pc.Collection)
            .Where(p => p.Activo && p.Estado == "ACTIVO" && p.Visibilidad != "SOLO_SUCURSAL")
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(busqueda))
            query = query.Where(p =>
                p.Nombre.Contains(busqueda) || p.Descripcion.Contains(busqueda)
            );

        if (!string.IsNullOrWhiteSpace(categoria))
            query = query.Where(p =>
                p.ProductCategories.Any(pc => pc.Category.Nombre.ToLower() == categoria.ToLower())
            );

        if (!string.IsNullOrWhiteSpace(coleccion))
            query = query.Where(p =>
                p.ProductCollections.Any(pc =>
                    pc.Collection.Nombre.ToLower() == coleccion.ToLower()
                )
            );

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(p => p.Nombre)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(p => new ProductSummaryDto
            {
                Id = p.Id,
                Nombre = p.Nombre,
                PrecioBase = p.PrecioBase,
                Tipo = p.Tipo,
                Estado = p.Estado,
                ImagenUrl = p.ImagenUrl,
                Stock = null,
            })
            .ToListAsync();

        return new PagedResultDto<ProductSummaryDto>
        {
            Items = items,
            Total = total,
            Pagina = page,
            TamanoPagina = size,
            TotalPaginas = (int)Math.Ceiling(total / (double)size),
        };
    }

    // ── Detalle público ───────────────────────────────────────────
    public async Task<ProductResponseDto> ObtenerPublicoAsync(Guid id)
    {
        var p =
            await _context
                .Products.Include(p => p.ProductCategories)
                .ThenInclude(pc => pc.Category)
                .Include(p => p.ProductCollections)
                .ThenInclude(pc => pc.Collection)
                .FirstOrDefaultAsync(p =>
                    p.Id == id
                    && p.Activo
                    && p.Estado == "ACTIVO"
                    && p.Visibilidad != "SOLO_SUCURSAL"
                ) ?? throw new NotFoundException("Producto", id);

        return MapToDto(p);
    }

    // ── Detalle admin ─────────────────────────────────────────────
    public async Task<ProductResponseDto> ObtenerAdminAsync(Guid id)
    {
        var p =
            await _context
                .Products.Include(p => p.ProductCategories)
                .ThenInclude(pc => pc.Category)
                .Include(p => p.ProductCollections)
                .ThenInclude(pc => pc.Collection)
                .FirstOrDefaultAsync(p => p.Id == id && p.Activo)
            ?? throw new NotFoundException("Producto", id);

        return MapToDto(p);
    }

    // ── Listar admin ──────────────────────────────────────────────
    public async Task<PagedResultDto<ProductSummaryDto>> ListarAdminAsync(
        string? busqueda,
        string? estado,
        int page,
        int size
    )
    {
        var query = _context.Products.Where(p => p.Activo).AsQueryable();

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
                Id = p.Id,
                Nombre = p.Nombre,
                PrecioBase = p.PrecioBase,
                Tipo = p.Tipo,
                Estado = p.Estado,
                ImagenUrl = p.ImagenUrl,
                Stock = null,
            })
            .ToListAsync();

        return new PagedResultDto<ProductSummaryDto>
        {
            Items = items,
            Total = total,
            Pagina = page,
            TamanoPagina = size,
            TotalPaginas = (int)Math.Ceiling(total / (double)size),
        };
    }

    // ── Crear ─────────────────────────────────────────────────────
    public async Task<ProductResponseDto> CrearAsync(CreateProductRequestDto request)
    {
        var producto = new Product
        {
            Id = Guid.NewGuid(),
            Nombre = request.Nombre.Trim(),
            Descripcion = request.Descripcion.Trim(),
            PrecioBase = request.PrecioBase,
            Tipo = request.Tipo.Trim().ToUpper(),
            EsPersonalizable = request.EsPersonalizable,
            Estado = request.Estado.Trim().ToUpper(),
            Visibilidad = request.Visibilidad.Trim().ToUpper(),
            ImagenUrl = request.ImagenUrl,
            CreadoEn = DateTime.UtcNow,
        };

        await AsignarCategorias(producto, request.Categorias);
        await AsignarColecciones(producto, request.Colecciones);
        await AsignarReceta(producto, request.Receta);

        _context.Products.Add(producto);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Producto creado: {Nombre} ({Id})", producto.Nombre, producto.Id);

        return await ObtenerConDetalleAsync(producto.Id);
    }

    // ── Actualizar ────────────────────────────────────────────────
    public async Task<ProductResponseDto> ActualizarAsync(Guid id, UpdateProductRequestDto request)
    {
        var producto =
            await _context.Products
                .Include(p => p.ProductCategories).ThenInclude(pc => pc.Category)
                .Include(p => p.ProductCollections).ThenInclude(pc => pc.Collection)
                .Include(p => p.ProductRecipes).ThenInclude(pr => pr.InventoryItem)
                .FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new NotFoundException("Producto", id);

        // Actualizar propiedades básicas
        if (!string.IsNullOrWhiteSpace(request.Nombre))
            producto.Nombre = request.Nombre.Trim();
        if (!string.IsNullOrWhiteSpace(request.Descripcion))
            producto.Descripcion = request.Descripcion.Trim();
        if (request.PrecioBase.HasValue)
            producto.PrecioBase = request.PrecioBase.Value;
        if (!string.IsNullOrWhiteSpace(request.Tipo))
            producto.Tipo = request.Tipo.Trim();
        if (request.EsPersonalizable.HasValue)
            producto.EsPersonalizable = request.EsPersonalizable.Value;
        if (!string.IsNullOrWhiteSpace(request.Estado))
            producto.Estado = request.Estado.Trim().ToUpper();
        if (!string.IsNullOrWhiteSpace(request.Visibilidad))
            producto.Visibilidad = request.Visibilidad.Trim().ToUpper();
        if (request.ImagenUrl != null)
            producto.ImagenUrl = request.ImagenUrl;
        if (request.Activo.HasValue)
            producto.Activo = request.Activo.Value;

        // 1. Actualizar Categorías (Sincronización quirúrgica por ID)
        if (request.Categorias != null)
        {
            var nombresLimpios = request.Categorias
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim().ToLower())
                .Distinct()
                .ToList();

            var categoriasDb = await _context.Categories
                .Where(c => nombresLimpios.Contains(c.Nombre.ToLower()))
                .ToListAsync();

            var idsDeseadas = categoriasDb.Select(c => c.Id).ToList();
            var idsActuales = producto.ProductCategories.Select(pc => pc.CategoryId).ToList();

            // Eliminar las que ya no deben estar
            var aEliminar = producto.ProductCategories
                .Where(pc => !idsDeseadas.Contains(pc.CategoryId))
                .ToList();
            foreach (var pc in aEliminar) _context.ProductCategories.Remove(pc);

            // Agregar solo las que no existían
            var aAgregarIds = idsDeseadas.Except(idsActuales);
            foreach (var catId in aAgregarIds)
            {
                producto.ProductCategories.Add(new ProductCategory { ProductId = producto.Id, CategoryId = catId });
            }
        }

        // 2. Actualizar Colecciones (Sincronización quirúrgica por ID)
        if (request.Colecciones != null)
        {
            var nombresLimpios = request.Colecciones
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim().ToLower())
                .Distinct()
                .ToList();

            var coleccionesDb = await _context.Collections
                .Where(c => nombresLimpios.Contains(c.Nombre.ToLower()))
                .ToListAsync();

            var idsDeseadas = coleccionesDb.Select(c => c.Id).ToList();
            var idsActuales = producto.ProductCollections.Select(pc => pc.CollectionId).ToList();

            // Eliminar
            var aEliminar = producto.ProductCollections
                .Where(pc => !idsDeseadas.Contains(pc.CollectionId))
                .ToList();
            foreach (var pc in aEliminar) _context.ProductCollections.Remove(pc);

            // Agregar
            var aAgregarIds = idsDeseadas.Except(idsActuales);
            foreach (var colId in aAgregarIds)
            {
                producto.ProductCollections.Add(new ProductCollection { ProductId = producto.Id, CollectionId = colId });
            }
        }

        // 3. Actualizar Receta (Mantenimiento de IDs de Receta)
        if (request.Receta != null)
        {
            var idsDeseadas = request.Receta.Select(r => r.InventoryItemId).ToList();
            var actualesReceta = producto.ProductRecipes.ToList();

            // Eliminar ausentes
            var aEliminar = actualesReceta.Where(r => !idsDeseadas.Contains(r.InventoryItemId)).ToList();
            foreach (var r in aEliminar) _context.ProductRecipes.Remove(r);

            // Actualizar o Agregar
            foreach (var itemReq in request.Receta)
            {
                var existente = actualesReceta.FirstOrDefault(r => r.InventoryItemId == itemReq.InventoryItemId);
                if (existente != null)
                {
                    existente.CantidadRequerida = itemReq.Cantidad;
                }
                else
                {
                    producto.ProductRecipes.Add(new ProductRecipe
                    {
                        Id = Guid.NewGuid(),
                        ProductId = producto.Id,
                        InventoryItemId = itemReq.InventoryItemId,
                        CantidadRequerida = itemReq.Cantidad
                    });
                }
            }
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!_context.Products.Any(p => p.Id == id))
                throw new NotFoundException("Producto", id);
            throw;
        }

        _logger.LogInformation("Producto actualizado correctamente: {Id}", id);
        return await ObtenerConDetalleAsync(id);
    }

    public async Task EliminarAsync(Guid id)
    {
        var producto =
            await _context.Products.FindAsync(id) ?? throw new NotFoundException("Producto", id);

        producto.Activo = false;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Producto desactivado (borrado lógico): {Id}", id);
    }

    // ── Helpers ───────────────────────────────────────────────────
    private async Task AsignarCategorias(Product producto, List<string> nombres)
    {
        foreach (var nombre in nombres)
        {
            var cat = await _context.Categories.FirstOrDefaultAsync(c =>
                c.Nombre.ToLower() == nombre.ToLower().Trim()
            );
            if (cat != null)
                producto.ProductCategories.Add(
                    new ProductCategory { ProductId = producto.Id, CategoryId = cat.Id }
                );
        }
    }

    private async Task AsignarColecciones(Product producto, List<string> nombres)
    {
        foreach (var nombre in nombres)
        {
            var col = await _context.Collections.FirstOrDefaultAsync(c =>
                c.Nombre.ToLower() == nombre.ToLower().Trim()
            );
            if (col != null)
                producto.ProductCollections.Add(
                    new ProductCollection { ProductId = producto.Id, CollectionId = col.Id }
                );
        }
    }

    private async Task AsignarReceta(Product producto, List<ProductRecipeRequestDto> receta)
    {
        foreach (var item in receta)
        {
            producto.ProductRecipes.Add(
                new ProductRecipe
                {
                    Id = Guid.NewGuid(),
                    ProductId = producto.Id,
                    InventoryItemId = item.InventoryItemId,
                    CantidadRequerida = item.Cantidad,
                }
            );
        }
    }

    private async Task<ProductResponseDto> ObtenerConDetalleAsync(Guid id)
    {
        var p = await _context
            .Products.Include(p => p.ProductCategories)
            .ThenInclude(pc => pc.Category)
            .Include(p => p.ProductCollections)
            .ThenInclude(pc => pc.Collection)
            .Include(p => p.ProductRecipes)
            .ThenInclude(pr => pr.InventoryItem)
            .FirstAsync(p => p.Id == id);
        return MapToDto(p);
    }

    private static ProductResponseDto MapToDto(Product p) =>
        new()
        {
            Id = p.Id,
            Nombre = p.Nombre,
            Descripcion = p.Descripcion,
            PrecioBase = p.PrecioBase,
            Tipo = p.Tipo,
            EsPersonalizable = p.EsPersonalizable,
            Estado = p.Estado,
            Visibilidad = p.Visibilidad,
            ImagenUrl = p.ImagenUrl,
            Activo = p.Activo,
            Categorias = p.ProductCategories.Select(pc => pc.Category.Nombre).ToList(),
            Colecciones = p.ProductCollections.Select(pc => pc.Collection.Nombre).ToList(),
            Receta = p
                .ProductRecipes.Select(pr => new RecipeItemDto
                {
                    InventoryItemId = pr.InventoryItemId,
                    Nombre = pr.InventoryItem.Nombre,
                    Cantidad = pr.CantidadRequerida,
                    PrecioCosto = pr.InventoryItem.PrecioCosto,
                    EsFlorPrimaria = pr.InventoryItem.EsFlorPrimaria,
                })
                .ToList(),
            CreadoEn = p.CreadoEn,
        };
}
