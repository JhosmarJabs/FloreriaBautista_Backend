using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Controllers.Admin;

[ApiController]
[Tags("1. Catálogo y Diseño")]
[Route("api/admin/customization-options")]
[Authorize(Roles = "ADMIN")]
public class AdminCustomizationOptionsController : ControllerBase
{
    private readonly AppDbContext _context;
    
    public AdminCustomizationOptionsController(AppDbContext context)
    {
        _context = context;
    }

    // GET /api/admin/customization-options
    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var items = await _context.CustomizationOptions.ToListAsync();
        return Ok(ApiResponseDto<List<CustomizationOption>>.Ok(items));
    }

    // POST /api/admin/customization-options (Crear)
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CustomizationOption request)
    {
        request.Id = Guid.NewGuid();
        _context.CustomizationOptions.Add(request);
        await _context.SaveChangesAsync();
        return Ok(ApiResponseDto<CustomizationOption>.Ok(request, "Opción guardada."));
    }

    // POST /api/admin/customization-options/{id} (Actualizar)
    [HttpPost("{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] CustomizationOption request)
    {
        var item = await _context.CustomizationOptions.FindAsync(id);
        if (item == null) return NotFound();

        item.Nombre          = request.Nombre          ?? item.Nombre;
        item.Tipo            = request.Tipo            ?? item.Tipo;
        item.PrecioAdicional = request.PrecioAdicional != 0 ? request.PrecioAdicional : item.PrecioAdicional;
        item.Activo          = request.Activo;

        await _context.SaveChangesAsync();
        return Ok(ApiResponseDto<CustomizationOption>.Ok(item, "Opción actualizada."));
    }
}
