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
        string? catalogo,
        int page,
        int size
    )
    {
        var query = _context
            .Products.Include(p => p.ProductCategories)
            .ThenInclude(pc => pc.Category)
            .Include(p => p.ProductCatalogos)
            .ThenInclude(pc => pc.Catalogo)
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

        if (!string.IsNullOrWhiteSpace(catalogo))
            query = query.Where(p =>
                p.ProductCatalogos.Any(pc =>
                    pc.Catalogo.Nombre.ToLower() == catalogo.ToLower()
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

    // ── Listar para empleado ──────────────────────────────────────
    // Igual que el listado público, pero SIN excluir "SOLO_SUCURSAL":
    // el empleado necesita ver también los productos de solo-tienda al
    // registrar un pedido físico. No expone datos internos (recetas, etc.).
    public async Task<PagedResultDto<ProductSummaryDto>> ListarParaEmpleadoAsync(
        string? busqueda,
        string? categoria,
        string? catalogo,
        int page,
        int size
    )
    {
        var query = _context
            .Products.Include(p => p.ProductCategories)
            .ThenInclude(pc => pc.Category)
            .Include(p => p.ProductCatalogos)
            .ThenInclude(pc => pc.Catalogo)
            .Where(p => p.Activo && p.Estado == "ACTIVO")
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(busqueda))
            query = query.Where(p =>
                p.Nombre.Contains(busqueda) || p.Descripcion.Contains(busqueda)
            );

        if (!string.IsNullOrWhiteSpace(categoria))
            query = query.Where(p =>
                p.ProductCategories.Any(pc => pc.Category.Nombre.ToLower() == categoria.ToLower())
            );

        if (!string.IsNullOrWhiteSpace(catalogo))
            query = query.Where(p =>
                p.ProductCatalogos.Any(pc =>
                    pc.Catalogo.Nombre.ToLower() == catalogo.ToLower()
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
                .Include(p => p.ProductCatalogos)
                .ThenInclude(pc => pc.Catalogo)
                .FirstOrDefaultAsync(p =>
                    p.Id == id
                    && p.Activo
                    && p.Estado == "ACTIVO"
                    && p.Visibilidad != "SOLO_SUCURSAL"
                ) ?? throw new NotFoundException("Producto", id);

        return MapToPublicDto(p);
    }

    private static ProductResponseDto MapToPublicDto(Product p) =>
        new()
        {
            Id = p.Id,
            Nombre = p.Nombre,
            Descripcion = p.Descripcion,
            PrecioBase = p.PrecioBase,
            Tipo = p.Tipo,
            EsPersonalizable = p.EsPersonalizable,
            Visibilidad = p.Visibilidad,
            ImagenUrl = p.ImagenUrl,
            Categorias = (p.ProductCategories ?? []).Select(pc => pc.Category?.Nombre ?? "Categoría").ToList(),
            Catalogos = (p.ProductCatalogos ?? []).Select(pc => pc.Catalogo?.Nombre ?? "Catálogo").ToList(),
        };

    // ── Detalle admin ─────────────────────────────────────────────
    public async Task<ProductResponseDto> ObtenerAdminAsync(Guid id)
    {
        var p =
            await _context
                .Products.Include(p => p.ProductCategories)
                .ThenInclude(pc => pc.Category)
                .Include(p => p.ProductCatalogos)
                .ThenInclude(pc => pc.Catalogo)
                .Include(p => p.ProductRecipes)
                .ThenInclude(pr => pr.InventoryItem)
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
            ActualizadoEn = DateTime.UtcNow,
        };

        await AsignarCategorias(producto, request.Categorias);
        await AsignarCatalogos(producto, request.Catalogos);
        await AsignarReceta(producto, request.Receta);

        _context.Products.Add(producto);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Producto creado: {Nombre} ({Id})", producto.Nombre, producto.Id);

        // Forzar recarga de navegación para el DTO de respuesta
        _context.Entry(producto).State = EntityState.Detached;
        return await ObtenerConDetalleAsync(producto.Id);
    }

    // ── Actualizar ────────────────────────────────────────────────
    public async Task<ProductResponseDto> ActualizarAsync(Guid id, UpdateProductRequestDto request)
    {
        Console.WriteLine($"[ProductService] Actualizando producto con ID: {id}");
        var producto =
            await _context.Products
                .Include(p => p.ProductCategories).ThenInclude(pc => pc.Category)
                .Include(p => p.ProductCatalogos).ThenInclude(pc => pc.Catalogo)
                .Include(p => p.ProductRecipes).ThenInclude(pr => pr.InventoryItem)
                .FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new NotFoundException("Producto", id);

        Console.WriteLine($"[ProductService] Producto cargado: {producto.Nombre} (ID: {producto.Id}). Categorías: {producto.ProductCategories.Count}, Catálogos: {producto.ProductCatalogos.Count}, Insumos en Receta: {producto.ProductRecipes.Count}");

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

        producto.ActualizadoEn = DateTime.UtcNow;

        // 1. Actualizar Categorías (Sincronización vía colección navegacional)
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
            
            // Eliminar las que ya no deben estar (usando la colección para que EF detecte el cambio)
            var aEliminar = producto.ProductCategories
                .Where(pc => !idsDeseadas.Contains(pc.CategoryId))
                .ToList();
            foreach (var pc in aEliminar) producto.ProductCategories.Remove(pc);

            // Agregar solo las que no existían
            var idsActuales = producto.ProductCategories.Select(pc => pc.CategoryId).ToList();
            var aAgregarIds = idsDeseadas.Except(idsActuales);
            foreach (var catId in aAgregarIds)
            {
                producto.ProductCategories.Add(new ProductCategory { ProductId = producto.Id, CategoryId = catId });
            }
        }

        // 2. Actualizar Catálogos
        if (request.Catalogos != null)
        {
            var nombresLimpios = request.Catalogos
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim().ToLower())
                .Distinct()
                .ToList();

            var catalogosDb = await _context.Catalogos
                .Where(c => nombresLimpios.Contains(c.Nombre.ToLower()))
                .ToListAsync();

            var idsDeseadas = catalogosDb.Select(c => c.Id).ToList();

            // Eliminar
            var aEliminar = producto.ProductCatalogos
                .Where(pc => !idsDeseadas.Contains(pc.CatalogoId))
                .ToList();
            foreach (var pc in aEliminar) producto.ProductCatalogos.Remove(pc);

            // Agregar
            var idsActuales = producto.ProductCatalogos.Select(pc => pc.CatalogoId).ToList();
            var aAgregarIds = idsDeseadas.Except(idsActuales);
            foreach (var catId in aAgregarIds)
            {
                producto.ProductCatalogos.Add(new ProductCatalogo { ProductId = producto.Id, CatalogoId = catId });
            }
        }

        // 3. Actualizar Receta
        if (request.Receta != null)
        {
            var idsDeseadas = request.Receta.Select(r => r.InventoryItemId).ToList();
            
            // Eliminar
            var aEliminar = producto.ProductRecipes
                .Where(r => !idsDeseadas.Contains(r.InventoryItemId))
                .ToList();
            foreach (var r in aEliminar) producto.ProductRecipes.Remove(r);

            // Actualizar o Agregar
            foreach (var itemReq in request.Receta)
            {
                var existente = producto.ProductRecipes.FirstOrDefault(r => r.InventoryItemId == itemReq.InventoryItemId);
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
        catch (DbUpdateConcurrencyException ex)
        {
            // Si el producto fue borrado físicamente por otro proceso
            if (!await _context.Products.AnyAsync(p => p.Id == id))
                throw new NotFoundException("Producto", id);

            _logger.LogWarning("Conflicto de concurrencia al actualizar producto {Id}. Refrescando entidades...", id);

            // Refrescar las entidades conflictivas con los valores reales de la BD
            foreach (var entry in ex.Entries)
            {
                var dbValues = await entry.GetDatabaseValuesAsync();
                if (dbValues == null)
                {
                    // La fila ya no existe en la BD, dejar de rastrearla
                    entry.State = EntityState.Detached;
                }
                else
                {
                    // Sobrescribir los valores originales con los de la BD para resolver el conflicto
                    entry.OriginalValues.SetValues(dbValues);
                }
            }

            // Reintentar el guardado una vez
            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Reintento exitoso para producto {Id}", id);
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogError("Reintento fallido para producto {Id}. El conflicto persiste.", id);
                throw new AppException("No se pudo actualizar el producto. Intenta recargar la página y volver a editarlo.");
            }
        }

        _logger.LogInformation("Producto actualizado correctamente: {Id}", id);
        
        // Forzar recarga de navegación para el DTO de respuesta
        _context.Entry(producto).State = EntityState.Detached;
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

    public async Task<ProductKpisDto> ObtenerKpisAsync()
    {
        var query = _context.Products.Where(p => p.Activo);

        var totalProductos = await query.CountAsync();
        var activos = await query.CountAsync(p => p.Estado == "ACTIVO");
        var borradores = await query.CountAsync(p => p.Estado == "BORRADOR");

        return new ProductKpisDto
        {
            TotalProductos = totalProductos,
            Activos = activos,
            Borradores = borradores
        };
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

    private async Task AsignarCatalogos(Product producto, List<string> nombres)
    {
        foreach (var nombre in nombres)
        {
            var cat = await _context.Catalogos.FirstOrDefaultAsync(c =>
                c.Nombre.ToLower() == nombre.ToLower().Trim()
            );
            if (cat != null)
                producto.ProductCatalogos.Add(
                    new ProductCatalogo { ProductId = producto.Id, CatalogoId = cat.Id }
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
            .Include(p => p.ProductCatalogos)
            .ThenInclude(pc => pc.Catalogo)
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
            Categorias = (p.ProductCategories ?? []).Select(pc => pc.Category?.Nombre ?? "Categoría Pendiente").ToList(),
            Catalogos = (p.ProductCatalogos ?? []).Select(pc => pc.Catalogo?.Nombre ?? "Catálogo Pendiente").ToList(),
            Receta = (p.ProductRecipes ?? [])
                .Select(pr => new RecipeItemDto
                {
                    InventoryItemId = pr.InventoryItemId,
                    Nombre = pr.InventoryItem?.Nombre ?? "Insumo no encontrado",
                    Cantidad = pr.CantidadRequerida,
                    PrecioCosto = pr.InventoryItem?.PrecioCosto ?? 0,
                    EsFlorPrimaria = pr.InventoryItem?.EsFlorPrimaria ?? false,
                })
                .ToList(),
            CreadoEn = p.CreadoEn,
            ActualizadoEn = p.ActualizadoEn
        };
}
