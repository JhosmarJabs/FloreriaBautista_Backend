using FloreriaBautista.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Controllers;

[ApiController]
[Tags("Público")]
[Route("api/[controller]")]
public class CollectionsController : ControllerBase
{
    private readonly AppDbContext _context;

    public CollectionsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetCollections()
    {
        var collections = await _context.Collections
            .Where(c => c.Activo && c.Estado == "ACTIVA")
            .Select(c => new 
            { 
                c.Id, 
                c.Nombre, 
                c.Descripcion 
            })
            .ToListAsync();

        return Ok(collections);
    }
}
