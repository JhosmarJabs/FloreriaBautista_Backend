using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Common;
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
        var items = await _context.Catalogos.OrderBy(x => x.Nombre).ToListAsync();
        return Ok(ApiResponseDto<List<Catalogo>>.Ok(items));
    }

    // GET /api/admin/catalogos/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detalle(Guid id)
    {
        var item = await _context.Catalogos.FindAsync(id);
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

        // Limpiar relaciones anteriores
        _context.ProductCatalogos.RemoveRange(item.ProductCatalogos);

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
