using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Controllers.Admin;

[ApiController]
[Tags("1. Catálogo y Diseño")]
[Route("api/admin/categories")]
[Authorize(Roles = "ADMIN")]
public class AdminCategoriesController : ControllerBase
{
    private readonly AppDbContext _context;
    
    public AdminCategoriesController(AppDbContext context)
    {
        _context = context;
    }

    // GET /api/admin/categories
    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var items = await _context.Categories.OrderBy(x => x.Nombre).ToListAsync();
        return Ok(ApiResponseDto<List<Category>>.Ok(items));
    }

    // GET /api/admin/categories/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detalle(Guid id)
    {
        var item = await _context.Categories.FindAsync(id);
        if (item == null) return NotFound(ApiResponseDto<object>.Fail("Categoría no encontrada."));
        return Ok(ApiResponseDto<Category>.Ok(item));
    }

    // POST /api/admin/categories (Crear)
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] Category request)
    {
        request.Id = Guid.NewGuid();
        request.CreadoEn = DateTime.UtcNow;
        request.ActualizadoEn = DateTime.UtcNow;
        _context.Categories.Add(request);
        await _context.SaveChangesAsync();
        return Ok(ApiResponseDto<Category>.Ok(request, "Categoría creada correctamente."));
    }

    // POST /api/admin/categories/{id} (Actualizar/Eliminar lógico)
    [HttpPost("{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] Category request)
    {
        var item = await _context.Categories.FindAsync(id);
        if (item == null) return NotFound(ApiResponseDto<object>.Fail("Categoría no encontrada."));

        // Actualización parcial simulada por ahora (o directa si viene el objeto)
        item.Nombre = request.Nombre ?? item.Nombre;
        item.Descripcion = request.Descripcion ?? item.Descripcion;
        item.Estado = request.Estado ?? item.Estado;
        item.ImagenUrl = request.ImagenUrl ?? item.ImagenUrl;
        item.Activo = request.Activo; // Borrado lógico aquí
        item.ActualizadoEn = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(ApiResponseDto<Category>.Ok(item, "Categoría actualizada correctamente."));
    }
}
