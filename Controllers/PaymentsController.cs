using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Orders;
using FloreriaBautista.Models.DTOs.Payments;
using FloreriaBautista.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Controllers;

[ApiController]
[Tags("Pagos")]
[Route("api/payments/mercadopago")]
public class PaymentsController : ControllerBase
{
    private readonly IMercadoPagoService _mp;
    private readonly IOrderService       _orders;
    private readonly AppDbContext         _context;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        IMercadoPagoService mp, IOrderService orders, AppDbContext context,
        ILogger<PaymentsController> logger)
    {
        _mp      = mp;
        _orders  = orders;
        _context = context;
        _logger  = logger;
    }

    // POST /api/payments/mercadopago/preference
    // Crea la preferencia de Checkout Pro para una orden ya creada del cliente.
    [HttpPost("preference")]
    [Authorize]
    public async Task<IActionResult> CrearPreferencia([FromBody] CreatePreferenceRequestDto request)
    {
        var userId = ObtenerUsuarioId();
        if (userId == null)
            return Unauthorized(ApiResponseDto<object>.Fail("No autenticado."));

        if (!_mp.EstaConfigurado)
            return StatusCode(503, ApiResponseDto<object>.Fail("El pago en línea no está disponible por el momento."));

        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId.Value);
        if (customer == null)
            return NotFound(ApiResponseDto<object>.Fail("Cliente no encontrado."));

        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId);

        if (order == null)
            return NotFound(ApiResponseDto<object>.Fail("Pedido no encontrado."));
        if (order.CustomerId != customer.Id)
            return Forbid();
        if (order.SaldoPendiente <= 0)
            return BadRequest(ApiResponseDto<object>.Fail("Este pedido ya está pagado."));

        // Nombres de producto para mostrarlos en el checkout de Mercado Pago.
        var productIds = order.OrderItems.Select(i => i.ProductId).Distinct().ToList();
        var nombres = await _context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Nombre);

        var items = order.OrderItems.Select(i => new MpPreferenceItem(
            nombres.TryGetValue(i.ProductId, out var n) ? n : "Producto",
            i.Cantidad,
            i.PrecioUnitario)).ToList();

        // El envío se agrega como un renglón para que el total coincida con la orden.
        if (order.CostoEnvio is > 0)
            items.Add(new MpPreferenceItem("Envío a domicilio", 1, order.CostoEnvio.Value));

        var pref = await _mp.CrearPreferenciaAsync(
            order.Id, $"Pedido {order.Id}", items, customer.Correo);

        return Ok(ApiResponseDto<PreferenceResponseDto>.Ok(pref));
    }

    // POST /api/payments/mercadopago/confirm
    // Confirmación sincrónica al regresar de Mercado Pago (funciona sin webhook público).
    [HttpPost("confirm")]
    [Authorize]
    public async Task<IActionResult> Confirmar([FromBody] ConfirmPaymentRequestDto request)
    {
        var userId = ObtenerUsuarioId();
        if (userId == null)
            return Unauthorized(ApiResponseDto<object>.Fail("No autenticado."));

        var info = await _mp.ConsultarPagoAsync(request.PaymentId);
        if (info == null)
            return NotFound(ApiResponseDto<object>.Fail("No se encontró el pago."));

        var resultado = await ProcesarPagoAsync(info);
        return Ok(ApiResponseDto<PaymentResultDto>.Ok(resultado));
    }

    // POST /api/payments/mercadopago/confirm-order
    // Confirma el pago buscándolo en MP por el id de la orden (sin payment_id ni webhook).
    // Se usa cuando el cliente vuelve a la tienda pero MP no redirigió con datos (ej. localhost).
    [HttpPost("confirm-order")]
    [Authorize]
    public async Task<IActionResult> ConfirmarPorOrden([FromBody] CreatePreferenceRequestDto request)
    {
        var userId = ObtenerUsuarioId();
        if (userId == null)
            return Unauthorized(ApiResponseDto<object>.Fail("No autenticado."));

        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId.Value);
        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == request.OrderId);
        if (customer == null || order == null || order.CustomerId != customer.Id)
            return NotFound(ApiResponseDto<object>.Fail("Pedido no encontrado."));

        // Si ya está pagado, respondemos acreditado (idempotente).
        if (order.SaldoPendiente <= 0)
            return Ok(ApiResponseDto<PaymentResultDto>.Ok(new PaymentResultDto
            { OrderId = order.Id, Estado = "approved", Acreditado = true }));

        var info = await _mp.BuscarPagoAprobadoPorOrdenAsync(order.Id);
        if (info == null)
            return Ok(ApiResponseDto<PaymentResultDto>.Ok(new PaymentResultDto
            { OrderId = order.Id, Estado = "pending", Acreditado = false }));

        var resultado = await ProcesarPagoAsync(info);
        return Ok(ApiResponseDto<PaymentResultDto>.Ok(resultado));
    }

    // POST /api/payments/mercadopago/webhook
    // Notificación asíncrona de Mercado Pago (producción). Siempre responde 200.
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook()
    {
        // MP manda el id del pago por query (?type=payment&data.id=123) o en el body.
        var tipo = Request.Query["type"].ToString();
        var paymentId = Request.Query["data.id"].ToString();

        if (string.IsNullOrEmpty(paymentId))
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                var raw = await reader.ReadToEndAsync();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(raw);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("type", out var t)) tipo = t.GetString() ?? tipo;
                    if (root.TryGetProperty("data", out var d) && d.TryGetProperty("id", out var idEl))
                        paymentId = idEl.GetString() ?? idEl.ToString();
                }
            }
            catch { /* body no-JSON: se ignora */ }
        }

        if (tipo == "payment" && !string.IsNullOrEmpty(paymentId))
        {
            try
            {
                var info = await _mp.ConsultarPagoAsync(paymentId);
                if (info != null) await ProcesarPagoAsync(info);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando webhook de pago {PaymentId}", paymentId);
            }
        }

        // Siempre 200 para que Mercado Pago no reintente indefinidamente.
        return Ok();
    }

    // Acredita un pago aprobado a su orden, de forma idempotente.
    private async Task<PaymentResultDto> ProcesarPagoAsync(MpPaymentInfo info)
    {
        var resultado = new PaymentResultDto { Estado = info.Status, Monto = info.Amount };

        if (!Guid.TryParse(info.ExternalReference, out var orderId))
            return resultado;
        resultado.OrderId = orderId;

        if (info.Status != "approved")
            return resultado;

        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null) return resultado;

        // Idempotencia: si ya no hay saldo, el pago ya fue acreditado (por webhook o confirm).
        if (order.SaldoPendiente <= 0)
        {
            resultado.Acreditado = true;
            return resultado;
        }

        var monto = Math.Min(info.Amount > 0 ? info.Amount : order.SaldoPendiente, order.SaldoPendiente);
        await _orders.RegistrarPagoAsync(orderId, new RegisterPaymentRequestDto
        {
            Monto  = monto,
            Metodo = "TARJETA",
        });

        _logger.LogInformation("Pago Mercado Pago acreditado: orden {OrderId}, monto {Monto}", orderId, monto);
        resultado.Acreditado = true;
        return resultado;
    }

    private Guid? ObtenerUsuarioId()
    {
        var claim = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
