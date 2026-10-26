using FloreriaBautista.Data;
using FloreriaBautista.Extensions;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Eventos;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Services.Eventos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Controllers;

[ApiController]
[Tags("Eventos")]
[Route("api/solicitudes-evento")]
[Authorize(Roles = "CLIENTE")]
public class SolicitudesEventoController : ControllerBase
{
    private readonly ISolicitudEventoService _servicio;
    private readonly AppDbContext _context;

    public SolicitudesEventoController(ISolicitudEventoService servicio, AppDbContext context)
    {
        _servicio = servicio;
        _context  = context;
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearSolicitudEventoDto dto)
    {
        var userId = User.UsuarioIdRequerido();
        // El CustomerId sale siempre del token, nunca del body.
        var customerId = await _context.Customers
            .Where(c => c.UserId == userId)
            .Select(c => c.Id)
            .FirstOrDefaultAsync();

        if (customerId == Guid.Empty)
            return BadRequest(ApiResponseDto<object>.Fail("No se encontró tu perfil de cliente."));

        try
        {
            var creada = await _servicio.CrearAsync(customerId, dto);
            return StatusCode(StatusCodes.Status201Created, ApiResponseDto<SolicitudEventoDto>.Ok(creada));
        }
        catch (AppException ex) when (ex is not NotFoundException)
        {
            return BadRequest(ApiResponseDto<object>.Fail(ex.Message));
        }
    }
}
