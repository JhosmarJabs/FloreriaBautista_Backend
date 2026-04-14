using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FloreriaBautista.Controllers;

[ApiController]
[Tags("Administrador")]
[Route("api/admin/collections")]
[Authorize(Roles = "ADMIN")]
public class AdminCollectionsController : ControllerBase
{
    private readonly AppDbContext _context;
    
    public AdminCollectionsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> Guardar([FromBody] Collection request)
    {
        if (request.Id == Guid.Empty)
        {
            request.Id = Guid.NewGuid();
            _context.Collections.Add(request);
        }
        else
        {
            _context.Collections.Update(request);
        }

        await _context.SaveChangesAsync();
        return Ok(ApiResponseDto<Collection>.Ok(request, "Colección guardada correctamente."));
    }
    [HttpPost("{id:guid}/delete")]
    public async Task<IActionResult> Eliminar(Guid id)
    {
        var col = await _context.Collections.FindAsync(id);
        if (col == null) return NotFound();
        
        col.Activo = false;
        await _context.SaveChangesAsync();
        return Ok(ApiResponseDto<object>.Ok(null!, "Colección desactivada correctamente."));
    }
}
