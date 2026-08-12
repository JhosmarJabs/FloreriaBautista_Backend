using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Orders;
using FloreriaBautista.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FloreriaBautista.Controllers.Admin;

[ApiController]
[Tags("2. Operaciones y Ventas")]
[Route("api/admin/orders")]
[Authorize(Roles = "ADMIN,EMPLEADO")]
public class AdminOrdersController : ControllerBase
{
    private readonly IOrderService   _orderService;
    private readonly IOrderArchiver  _orderArchiver;

    public AdminOrdersController(IOrderService orderService, IOrderArchiver orderArchiver)
    {
        _orderService  = orderService;
        _orderArchiver = orderArchiver;
    }

    // GET /api/admin/orders?estado=PENDIENTE_VALIDACION&desde=2026-01-01&hasta=2026-12-31&archivado=false
    // requierenCierre=true → pedidos archivados que siguen EN_RUTA y nadie cerró
    // (ignora 'archivado', porque esa vista siempre sale del archivo).
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string?  estado,
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        [FromQuery] bool archivado = false,
        [FromQuery] bool requierenCierre = false)
    {
        var resultado = await _orderService.ListarAdminAsync(
            estado, desde, hasta, page, size, archivado, requierenCierre);
        return Ok(ApiResponseDto<PagedResultDto<OrderSummaryDto>>.Ok(resultado));
    }

    // POST /api/admin/orders/archivar-atrasados
    // Fuerza de inmediato la pasada que el scheduler hace cada hora, sin reiniciar
    // el backend. Devuelve cuántos pedidos movió y cómo quedaron.
    [HttpPost("archivar-atrasados")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> ArchivarAtrasados()
    {
        var claim = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        Guid? usuarioId = Guid.TryParse(claim, out var id) ? id : null;

        var resultado = await _orderArchiver.ArchivarAtrasadosAsync(usuarioId);
        return Ok(ApiResponseDto<ArchivadoResultDto>.Ok(resultado,
            $"{resultado.Total} pedido(s) atrasado(s) movidos al archivo."));
    }

    // GET /api/admin/orders/{orderId}
    [HttpGet("{orderId:guid}")]
    public async Task<IActionResult> Detalle(Guid orderId)
    {
        var order = await _orderService.ObtenerAdminAsync(orderId);
        return Ok(ApiResponseDto<OrderResponseDto>.Ok(order));
    }
}
