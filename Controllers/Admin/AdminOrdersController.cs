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
    private readonly IOrderService _orderService;
    public AdminOrdersController(IOrderService orderService) => _orderService = orderService;

    // GET /api/admin/orders?estado=PENDIENTE_VALIDACION&desde=2026-01-01&hasta=2026-12-31
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string?  estado,
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var resultado = await _orderService.ListarAdminAsync(estado, desde, hasta, page, size);
        return Ok(ApiResponseDto<PagedResultDto<OrderSummaryDto>>.Ok(resultado));
    }

    // GET /api/admin/orders/{orderId}
    [HttpGet("{orderId:guid}")]
    public async Task<IActionResult> Detalle(Guid orderId)
    {
        var order = await _orderService.ObtenerAdminAsync(orderId);
        return Ok(ApiResponseDto<OrderResponseDto>.Ok(order));
    }
}
