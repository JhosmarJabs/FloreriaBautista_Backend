using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.SupplyOrders;
using FloreriaBautista.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FloreriaBautista.Controllers.Admin;

[ApiController]
[Tags("2. Operaciones y Ventas")]
[Route("api/admin/supply-orders")]
[Authorize(Roles = "ADMIN")]
public class AdminSupplyOrdersController : ControllerBase
{
    private readonly ISupplyOrderService _service;
    public AdminSupplyOrdersController(ISupplyOrderService service) => _service = service;

    // GET /api/admin/supply-orders?estado=ENVIADA&desde=2026-08-01&hasta=2026-08-31
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string?   estado,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var resultado = await _service.ListarAsync(estado, desde, hasta, page, size);
        return Ok(ApiResponseDto<PagedResultDto<SupplyOrderListItemDto>>.Ok(resultado));
    }

    // GET /api/admin/supply-orders/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detalle(Guid id)
    {
        var solicitud = await _service.ObtenerAsync(id);
        return Ok(ApiResponseDto<SupplyOrderDetailDto>.Ok(solicitud));
    }

    // POST /api/admin/supply-orders
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CreateSupplyOrderDto request)
    {
        var usuarioId = ObtenerUsuarioId();
        if (usuarioId == null) return Unauthorized();

        var solicitud = await _service.CrearAsync(request, usuarioId.Value);
        return Ok(ApiResponseDto<SupplyOrderDetailDto>.Ok(
            solicitud, $"Solicitud {solicitud.Folio} creada correctamente."));
    }

    // PUT /api/admin/supply-orders/{id} (solo en BORRADOR)
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] UpdateSupplyOrderDto request)
    {
        var usuarioId = ObtenerUsuarioId();
        if (usuarioId == null) return Unauthorized();

        var solicitud = await _service.ActualizarAsync(id, request, usuarioId.Value);
        return Ok(ApiResponseDto<SupplyOrderDetailDto>.Ok(solicitud, "Solicitud actualizada."));
    }

    // POST /api/admin/supply-orders/{id}/enviar
    [HttpPost("{id:guid}/enviar")]
    public async Task<IActionResult> Enviar(Guid id)
    {
        var usuarioId = ObtenerUsuarioId();
        if (usuarioId == null) return Unauthorized();

        var solicitud = await _service.EnviarAsync(id, usuarioId.Value);
        return Ok(ApiResponseDto<SupplyOrderDetailDto>.Ok(
            solicitud, $"Solicitud {solicitud.Folio} marcada como enviada."));
    }

    // POST /api/admin/supply-orders/{id}/recepcion
    // Confirma línea por línea qué llegó y genera las entradas al inventario.
    [HttpPost("{id:guid}/recepcion")]
    public async Task<IActionResult> RegistrarRecepcion(Guid id, [FromBody] ReceiveSupplyOrderDto request)
    {
        var usuarioId = ObtenerUsuarioId();
        if (usuarioId == null) return Unauthorized();

        var solicitud = await _service.RegistrarRecepcionAsync(id, request, usuarioId.Value);
        return Ok(ApiResponseDto<SupplyOrderDetailDto>.Ok(solicitud, "Recepción registrada."));
    }

    // POST /api/admin/supply-orders/{id}/cancelar
    [HttpPost("{id:guid}/cancelar")]
    public async Task<IActionResult> Cancelar(Guid id, [FromBody] CancelSupplyOrderDto request)
    {
        var usuarioId = ObtenerUsuarioId();
        if (usuarioId == null) return Unauthorized();

        var solicitud = await _service.CancelarAsync(id, request, usuarioId.Value);
        return Ok(ApiResponseDto<SupplyOrderDetailDto>.Ok(
            solicitud, $"Solicitud {solicitud.Folio} cancelada."));
    }

    private Guid? ObtenerUsuarioId()
    {
        var claim = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
