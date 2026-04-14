using FloreriaBautista.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Controllers;

[ApiController]
[Tags("Público")]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly AppDbContext _context;

    public CategoriesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _context.Categories
            .Where(c => c.Activo && c.Estado == "ACTIVA")
            .Select(c => new 
            { 
                c.Id, 
                c.Nombre, 
                c.Descripcion 
            })
            .ToListAsync();

        return Ok(categories);
    }
}
