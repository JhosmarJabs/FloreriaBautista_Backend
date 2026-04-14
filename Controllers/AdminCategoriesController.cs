using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FloreriaBautista.Controllers;

[ApiController]
[Tags("Administrador")]
[Route("api/admin/categories")]
[Authorize(Roles = "ADMIN")]
public class AdminCategoriesController : ControllerBase
{
    private readonly AppDbContext _context;
    
    public AdminCategoriesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> Guardar([FromBody] Category request)
    {
        if (request.Id == Guid.Empty)
        {
            request.Id = Guid.NewGuid();
            _context.Categories.Add(request);
        }
        else
        {
            _context.Categories.Update(request);
        }

        await _context.SaveChangesAsync();
        return Ok(ApiResponseDto<Category>.Ok(request, "Categoría guardada correctamente."));
    }
    [HttpPost("{id:guid}/delete")]
    public async Task<IActionResult> Eliminar(Guid id)
    {
        var cat = await _context.Categories.FindAsync(id);
        if (cat == null) return NotFound();
        
        cat.Activo = false;
        await _context.SaveChangesAsync();
        return Ok(ApiResponseDto<object>.Ok(null!, "Categoría desactivada correctamente."));
    }
}
