using FloreriaBautista.Extensions;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.InstantSales;
using FloreriaBautista.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FloreriaBautista.Controllers.Admin;

[ApiController]
[Tags("2. Operaciones y Ventas")]
[Route("api/admin/solicitudes-venta-instantanea")]
[Authorize(Roles = "ADMIN,EMPLEADO")]
public class AdminInstantSalesController : ControllerBase
{
    private readonly IInstantSaleService _instantSaleService;

    public AdminInstantSalesController(IInstantSaleService instantSaleService)
    {
        _instantSaleService = instantSaleService;
    }

    /// <summary>
    /// GET /api/admin/solicitudes-venta-instantanea?estado=PENDIENTE
    /// Listado con filtro por estado, paginado.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string? estado,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var resultado = await _instantSaleService.ListarAdminAsync(estado, page, size);
        return Ok(ApiResponseDto<PagedResultDto<SolicitudVentaInstantaneaDto>>.Ok(resultado));
    }

    /// <summary>
    /// GET /api/admin/solicitudes-venta-instantanea/{id}
    /// Detalle de una solicitud.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obtener(Guid id)
    {
        var resultado = await _instantSaleService.ObtenerAsync(id);
        return Ok(ApiResponseDto<SolicitudVentaInstantaneaDto>.Ok(resultado));
    }

    /// <summary>
    /// POST /api/admin/solicitudes-venta-instantanea/{id}/aceptar
    /// Autorización dinámica: ADMIN o responsable de turno vigente (403 si no).
    /// Lock optimista: 409 si la solicitud ya fue decidida.
    /// Al aceptar: Estado = ACEPTADA, ReservaExpiraEn = ahora + 10min.
    /// </summary>
    [HttpPost("{id:guid}/aceptar")]
    public async Task<IActionResult> Aceptar(Guid id)
    {
        var usuarioId = User.UsuarioIdRequerido();
        var resultado = await _instantSaleService.AceptarAsync(id, usuarioId);
        return Ok(ApiResponseDto<SolicitudVentaInstantaneaDto>.Ok(resultado,
            "Solicitud aceptada. El cliente tiene 10 minutos para completar el pago."));
    }

    /// <summary>
    /// POST /api/admin/solicitudes-venta-instantanea/{id}/rechazar
    /// Mismo control de autorización y lock que /aceptar.
    /// MotivoRechazo es opcional (texto libre del decisor).
    /// </summary>
    [HttpPost("{id:guid}/rechazar")]
    public async Task<IActionResult> Rechazar(Guid id, [FromBody] RechazarSolicitudRequestDto? body)
    {
        var usuarioId = User.UsuarioIdRequerido();
        var resultado = await _instantSaleService.RechazarAsync(
            id, usuarioId, body?.MotivoRechazo);
        return Ok(ApiResponseDto<SolicitudVentaInstantaneaDto>.Ok(resultado,
            "Solicitud rechazada."));
    }
}
