using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Eventos;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Services.Eventos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FloreriaBautista.Controllers.Admin;

[ApiController]
[Tags("Eventos")]
[Route("api/admin/solicitudes-evento")]
[Authorize(Roles = "ADMIN")]
public class AdminSolicitudesEventoController : ControllerBase
{
    private readonly ISolicitudEventoService _servicio;

    public AdminSolicitudesEventoController(ISolicitudEventoService servicio) => _servicio = servicio;

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string? estado, [FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        try
        {
            var resultado = await _servicio.ListarAsync(estado, page, size);
            return Ok(ApiResponseDto<PagedResultDto<SolicitudEventoDto>>.Ok(resultado));
        }
        catch (AppException ex) when (ex is not NotFoundException)
        {
            return BadRequest(ApiResponseDto<object>.Fail(ex.Message));
        }
    }

    [HttpPatch("{id:guid}/estado")]
    public async Task<IActionResult> CambiarEstado(Guid id, [FromBody] CambiarEstadoSolicitudEventoDto dto)
    {
        try
        {
            var actualizada = await _servicio.CambiarEstadoAsync(id, dto.Estado);
            return Ok(ApiResponseDto<SolicitudEventoDto>.Ok(actualizada));
        }
        catch (NotFoundException)
        {
            return NotFound(ApiResponseDto<object>.Fail("Solicitud de evento no encontrada."));
        }
        catch (AppException ex)
        {
            return BadRequest(ApiResponseDto<object>.Fail(ex.Message));
        }
    }
}
