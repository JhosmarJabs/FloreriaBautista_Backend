using FloreriaBautista.Data;
using FloreriaBautista.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Controllers;

[ApiController]
[Tags("Público")]
[Route("api/catalogos")]
public class CatalogosController : ControllerBase
{
    private readonly AppDbContext _context;

    public CatalogosController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetCatalogos()
    {
        bool isAdmin = User.Identity?.IsAuthenticated == true && User.IsInRole("ADMIN");

        var query = _context.Catalogos.AsQueryable();

        if (!isAdmin)
        {
            query = query.Where(c => c.Activo && c.Estado == "ACTIVA");
        }

        var items = await query
            .OrderBy(c => c.Nombre)
            .Select(c => new 
            { 
                c.Id, 
                c.Nombre, 
                c.Descripcion,
                c.ImagenUrl,
                c.Activo,
                c.Estado
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCatalogo(Guid id)
    {
        var item = await _context.Catalogos.FindAsync(id);
        if (item == null)
        {
            return NotFound(new { mensaje = "Catálogo no encontrado." });
        }
        return Ok(item);
    }

    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Crear([FromBody] Catalogo request)
    {
        if (request == null)
        {
            return BadRequest(new { mensaje = "Los datos del catálogo son requeridos." });
        }

        request.Id = Guid.NewGuid();
        request.CreadoEn = DateTime.UtcNow;
        request.ActualizadoEn = DateTime.UtcNow;

        _context.Catalogos.Add(request);
        await _context.SaveChangesAsync();

        return Ok(request);
    }
}
