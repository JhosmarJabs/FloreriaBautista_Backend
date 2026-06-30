using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Products;
using FloreriaBautista.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Controllers.Admin;

[ApiController]
[Tags("1. Catálogo y Diseño")]
[Route("api/admin/catalogos")]
[Authorize(Roles = "ADMIN")]
public class AdminCatalogosController : ControllerBase
{
    private readonly AppDbContext _context;
    
    public AdminCatalogosController(AppDbContext context)
    {
        _context = context;
    }

    // GET /api/admin/catalogos
    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var items = await _context.Catalogos
            .Include(c => c.ProductCatalogos)
            .OrderBy(x => x.Nombre)
            .ToListAsync();
        return Ok(ApiResponseDto<List<Catalogo>>.Ok(items));
    }

    // GET /api/admin/catalogos/kpis
    [HttpGet("kpis")]
    public async Task<IActionResult> ObtenerKpis()
    {
        var queryCatalogos = _context.Catalogos.Where(c => c.Activo);

        var totalCatalogos = await queryCatalogos.CountAsync();
        var catalogosActivos = await queryCatalogos.CountAsync(c => c.Estado == "ACTIVA");

        var totalProductosListados = await _context.ProductCatalogos
            .Where(pc => pc.Catalogo.Activo && pc.Product.Activo)
            .Select(pc => pc.ProductId)
            .Distinct()
            .CountAsync();

        var kpis = new CatalogoKpisDto
        {
            TotalCatalogos = totalCatalogos,
            CatalogosActivos = catalogosActivos,
            TotalProductosListados = totalProductosListados
        };

        return Ok(ApiResponseDto<CatalogoKpisDto>.Ok(kpis));
    }

    // GET /api/admin/catalogos/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detalle(Guid id)
    {
        var item = await _context.Catalogos
            .Include(c => c.ProductCatalogos)
            .FirstOrDefaultAsync(c => c.Id == id);
            
        if (item == null) return NotFound(ApiResponseDto<object>.Fail("Catálogo no encontrado."));
        return Ok(ApiResponseDto<Catalogo>.Ok(item));
    }

    // POST /api/admin/catalogos (Crear)
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] Catalogo request)
    {
        request.Id = Guid.NewGuid();
        request.CreadoEn = DateTime.UtcNow;
        request.ActualizadoEn = DateTime.UtcNow;
        _context.Catalogos.Add(request);
        await _context.SaveChangesAsync();
        return Ok(ApiResponseDto<Catalogo>.Ok(request, "Catálogo creado correctamente."));
    }

    // POST /api/admin/catalogos/{id} (Actualizar/Eliminar lógico)
    [HttpPost("{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] Catalogo request)
    {
        var item = await _context.Catalogos
            .Include(c => c.ProductCatalogos)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (item == null) return NotFound(ApiResponseDto<object>.Fail("Catálogo no encontrado."));

        item.Nombre = request.Nombre ?? item.Nombre;
        item.Descripcion = request.Descripcion ?? item.Descripcion;
        item.ImagenUrl = request.ImagenUrl ?? item.ImagenUrl;
        item.Activo = request.Activo;
        item.ActualizadoEn = DateTime.UtcNow;

        // Limpiar relaciones anteriores en memoria y dejar que EF Core las rastree como eliminadas
        item.ProductCatalogos.Clear();

        // Agregar las nuevas relaciones
        if (request.ProductCatalogos != null)
        {
            foreach (var pc in request.ProductCatalogos)
            {
                item.ProductCatalogos.Add(new ProductCatalogo
                {
                    ProductId = pc.ProductId,
                    CatalogoId = id
                });
            }
        }

        await _context.SaveChangesAsync();
        return Ok(ApiResponseDto<Catalogo>.Ok(item, "Catálogo actualizado correctamente."));
    }
}
