using FloreriaBautista.Extensions;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.InstantSales;
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
    private readonly IOrderService        _orderService;
    private readonly IOrderArchiver       _orderArchiver;
    private readonly IInstantSaleExpirer  _instantSaleExpirer;

    public AdminOrdersController(
        IOrderService orderService,
        IOrderArchiver orderArchiver,
        IInstantSaleExpirer instantSaleExpirer)
    {
        _orderService       = orderService;
        _orderArchiver      = orderArchiver;
        _instantSaleExpirer = instantSaleExpirer;
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
        // El rol EMPLEADO comparte este endpoint con el admin (lo consume la web
        // en pages/employee/OrdersPage.tsx), pero NO comparte su alcance: se le
        // sirve su propia vista recortada y se ignoran 'desde', 'hasta',
        // 'archivado' y 'requierenCierre', que son las tres puertas por las que
        // podría alcanzar días anteriores o el trabajo de sus compañeros.
        if (!User.EsAdmin())
        {
            var soloSuyo = await _orderService.ListarEmpleadoAsync(
                User.UsuarioIdRequerido(), estado, page, size);
            return Ok(ApiResponseDto<PagedResultDto<OrderSummaryDto>>.Ok(soloSuyo));
        }

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

    // POST /api/admin/orders/expirar-solicitudes-instantaneas
    // Fuerza de inmediato la pasada del expirador de solicitudes de venta
    // instantanea, sin esperar el siguiente ciclo del scheduler (cada 30s).
    // Mismo patron que archivar-atrasados.
    [HttpPost("expirar-solicitudes-instantaneas")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> ExpirarSolicitudesInstantaneas()
    {
        var claim = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        Guid? usuarioId = Guid.TryParse(claim, out var id) ? id : null;

        var resultado = await _instantSaleExpirer.RevisarPendientesAsync(usuarioId);
        var total = resultado.Escaladas + resultado.ExpiradasSinRespuesta + resultado.ReservasLiberadas;
        return Ok(ApiResponseDto<ExpiracionResultDto>.Ok(resultado,
            $"{total} solicitud(es) procesada(s): {resultado.Escaladas} escalada(s), " +
            $"{resultado.ExpiradasSinRespuesta} expirada(s), {resultado.ReservasLiberadas} reserva(s) liberada(s)."));
    }

    // GET /api/admin/orders/delta?desde=<ISO-8601> — pedidos modificados desde la fecha
    [HttpGet("delta")]
    public async Task<IActionResult> Delta([FromQuery] DateTime desde)
    {
        var resultado = await _orderService.ListarDeltaAdminAsync(desde);
        return Ok(ApiResponseDto<IndexResultDto<OrderSummaryDto>>.Ok(resultado));
    }

    // GET /api/admin/orders/{orderId}
    [HttpGet("{orderId:guid}")]
    public async Task<IActionResult> Detalle(Guid orderId)
    {
        var order = User.EsAdmin()
            ? await _orderService.ObtenerAdminAsync(orderId)
            : await _orderService.ObtenerParaEmpleadoAsync(User.UsuarioIdRequerido(), orderId);

        return Ok(ApiResponseDto<OrderResponseDto>.Ok(order));
    }
}
