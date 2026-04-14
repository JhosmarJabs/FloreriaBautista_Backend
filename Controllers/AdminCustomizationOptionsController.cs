using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FloreriaBautista.Controllers;

[ApiController]
[Tags("Administrador")]
[Route("api/admin/customization-options")]
[Authorize(Roles = "ADMIN")]
public class AdminCustomizationOptionsController : ControllerBase
{
    private readonly AppDbContext _context;
    
    public AdminCustomizationOptionsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> Guardar([FromBody] CustomizationOption request)
    {
        if (request.Id == Guid.Empty)
        {
            request.Id = Guid.NewGuid();
            _context.CustomizationOptions.Add(request);
        }
        else
        {
            _context.CustomizationOptions.Update(request);
        }

        await _context.SaveChangesAsync();
        return Ok(ApiResponseDto<CustomizationOption>.Ok(request, "Opción de personalización guardada."));
    }
}
