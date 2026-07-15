using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Orders;
using FloreriaBautista.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FloreriaBautista.Controllers;

[ApiController]
[Tags("Privado o Cliente")]
[Route("api/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    public OrdersController(IOrderService orderService) => _orderService = orderService;

    // POST /api/orders
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CreateOrderRequestDto request)
    {
        var userId = ObtenerUsuarioId();
        if (userId == null) return Unauthorized();
        var order = await _orderService.CrearPedidoClienteAsync(userId.Value, request);
        return Ok(ApiResponseDto<OrderResponseDto>.Ok(order, "Pedido creado correctamente."));
    }

    // POST /api/orders/physical
    [HttpPost("physical")]
    [Authorize(Roles = "ADMIN,EMPLEADO")]
    public async Task<IActionResult> CrearFisico([FromBody] CreatePhysicalOrderRequestDto request)
    {
        var order = await _orderService.CrearPedidoFisicoAsync(request);
        return Ok(ApiResponseDto<OrderResponseDto>.Ok(order, "Pedido físico creado correctamente."));
    }

    // GET /api/orders/my
    [HttpGet("my")]
    public async Task<IActionResult> MisPedidos([FromQuery] int page = 1, [FromQuery] int size = 10)
    {
        var userId = ObtenerUsuarioId();
        if (userId == null) return Unauthorized();
        var resultado = await _orderService.ListarMisPedidosAsync(userId.Value, page, size);
        return Ok(ApiResponseDto<PagedResultDto<OrderSummaryDto>>.Ok(resultado));
    }

    // GET /api/orders/{orderId}
    [HttpGet("{orderId:guid}")]
    public async Task<IActionResult> Detalle(Guid orderId)
    {
        var userId = ObtenerUsuarioId();
        if (userId == null) return Unauthorized();
        var order = await _orderService.ObtenerMiPedidoAsync(userId.Value, orderId);
        return Ok(ApiResponseDto<OrderResponseDto>.Ok(order));
    }

    // POST /api/orders/{orderId:guid} (Actualizar estado)
    [HttpPost("{orderId:guid}")]
    [Authorize(Roles = "ADMIN,EMPLEADO")]
    public async Task<IActionResult> Actualizar(Guid orderId, [FromBody] UpdateOrderStatusRequestDto request)
    {
        var roles = User.FindAll(System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value).ToList();
        var order = await _orderService.CambiarEstadoAsync(orderId, request, roles);
        return Ok(ApiResponseDto<OrderResponseDto>.Ok(order, "Pedido actualizado (estado)."));
    }

    // POST /api/orders/{orderId}/payments (registrar anticipo o liquidación)
    [HttpPost("{orderId:guid}/payments")]
    [Authorize(Roles = "ADMIN,EMPLEADO")]
    public async Task<IActionResult> RegistrarPago(Guid orderId, [FromBody] RegisterPaymentRequestDto request)
    {
        var order = await _orderService.RegistrarPagoAsync(orderId, request);
        return Ok(ApiResponseDto<OrderResponseDto>.Ok(order, "Pago registrado correctamente."));
    }

    private Guid? ObtenerUsuarioId()
    {
        var claim = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
