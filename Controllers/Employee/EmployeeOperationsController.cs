using FloreriaBautista.Data;
using FloreriaBautista.Extensions;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Employee;
using FloreriaBautista.Models.DTOs.Orders;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Controllers.Employee;

/// <summary>
/// Superficie que consume la app móvil interna. Todo lo que hay aquí se resuelve
/// con el id del token y el día en curso de la tienda: ningún endpoint acepta un
/// rango de fechas ni un id de empleado, porque un parámetro que viaja en el
/// request es un parámetro que el cliente puede cambiar.
///
/// El admin también puede llamar a estos endpoints, y cuando lo hace ve SUS
/// propios registros, no los de todos. Para la vista global están los endpoints
/// bajo /api/admin.
/// </summary>
[ApiController]
[Tags("2. Operaciones y Ventas")]
[Route("api/employee")]
[Authorize(Roles = "ADMIN,EMPLEADO")]
public class EmployeeOperationsController : ControllerBase
{
    private readonly IOrderService           _orders;
    private readonly IEmployeeExpenseService _expenses;
    private readonly ICashCutService         _cashCuts;
    private readonly IErrorReportService     _errorReports;
    private readonly AppDbContext             _context;

    public EmployeeOperationsController(
        IOrderService           orders,
        IEmployeeExpenseService expenses,
        ICashCutService         cashCuts,
        IErrorReportService     errorReports,
        AppDbContext             context)
    {
        _orders       = orders;
        _expenses     = expenses;
        _cashCuts     = cashCuts;
        _errorReports = errorReports;
        _context      = context;
    }

    // ── Ventas ────────────────────────────────────────────────────

    /// <summary>
    /// Los pedidos que el empleado puede ver hoy: los que capturó él durante el
    /// día, más los que se entregan hoy aunque los haya capturado otro (si no,
    /// nadie podría entregar una venta anticipada tomada la semana pasada).
    /// </summary>
    // GET /api/employee/orders?estado=EN_RUTA&page=1&size=20
    [HttpGet("orders")]
    public async Task<IActionResult> MisVentas(
        [FromQuery] string? estado,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var resultado = await _orders.ListarEmpleadoAsync(User.UsuarioIdRequerido(), estado, page, size);
        return Ok(ApiResponseDto<PagedResultDto<OrderSummaryDto>>.Ok(resultado));
    }

    // GET /api/employee/orders/{orderId}
    [HttpGet("orders/{orderId:guid}")]
    public async Task<IActionResult> DetalleVenta(Guid orderId)
    {
        var order = await _orders.ObtenerParaEmpleadoAsync(User.UsuarioIdRequerido(), orderId);
        return Ok(ApiResponseDto<OrderResponseDto>.Ok(order));
    }

    // ── Gastos personales ─────────────────────────────────────────

    // GET /api/employee/expenses
    [HttpGet("expenses")]
    public async Task<IActionResult> MisGastos()
    {
        var gastos = await _expenses.ListarDelDiaAsync(User.UsuarioIdRequerido());
        return Ok(ApiResponseDto<List<ExpenseDto>>.Ok(gastos));
    }

    // POST /api/employee/expenses
    [HttpPost("expenses")]
    public async Task<IActionResult> RegistrarGasto([FromBody] CreateExpenseRequestDto request)
    {
        var gasto = await _expenses.RegistrarAsync(User.UsuarioIdRequerido(), request);
        return Ok(ApiResponseDto<ExpenseDto>.Ok(gasto, "Gasto registrado."));
    }

    // ── Corte de caja ─────────────────────────────────────────────

    /// <summary>Los números del día antes de cerrar, para cuadrar contra la caja.</summary>
    // GET /api/employee/cash-cut/preview
    [HttpGet("cash-cut/preview")]
    public async Task<IActionResult> PreviewCorte()
    {
        var preview = await _cashCuts.PreviewAsync(User.UsuarioIdRequerido());
        return Ok(ApiResponseDto<CashCutPreviewDto>.Ok(preview));
    }

    /// <summary>El corte de hoy, o null si todavía no cierra.</summary>
    // GET /api/employee/cash-cut
    [HttpGet("cash-cut")]
    public async Task<IActionResult> MiCorte()
    {
        var corte = await _cashCuts.ObtenerDelDiaAsync(User.UsuarioIdRequerido());
        return Ok(ApiResponseDto<CashCutDto?>.Ok(corte));
    }

    // POST /api/employee/cash-cut
    [HttpPost("cash-cut")]
    public async Task<IActionResult> CerrarCorte([FromBody] CreateCashCutRequestDto request)
    {
        var corte = await _cashCuts.CerrarAsync(User.UsuarioIdRequerido(), request);
        return Ok(ApiResponseDto<CashCutDto>.Ok(corte, "Corte de caja cerrado."));
    }

    // ── Reporte de errores ────────────────────────────────────────

    /// <summary>
    /// La única vía que tiene el empleado para corregir algo ya registrado. No
    /// modifica el registro: abre una petición que resuelve el administrador.
    /// </summary>
    // POST /api/employee/error-reports
    [HttpPost("error-reports")]
    public async Task<IActionResult> ReportarError([FromBody] CreateErrorReportRequestDto request)
    {
        var reporte = await _errorReports.CrearAsync(User.UsuarioIdRequerido(), request);
        return Ok(ApiResponseDto<ErrorReportDto>.Ok(reporte,
            "Reporte enviado. El administrador lo revisará."));
    }

    // GET /api/employee/error-reports
    [HttpGet("error-reports")]
    public async Task<IActionResult> MisReportes()
    {
        var reportes = await _errorReports.ListarMisPendientesAsync(User.UsuarioIdRequerido());
        return Ok(ApiResponseDto<List<ErrorReportDto>>.Ok(reportes));
    }

    // ── Push notifications ───────────────────────────────────────

    // POST /api/employee/push-token
    [HttpPost("push-token")]
    public async Task<IActionResult> RegistrarPushToken([FromBody] RegisterPushTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest(ApiResponseDto<object>.Fail("El token es obligatorio."));

        var userId = User.UsuarioIdRequerido();

        // Un token por dispositivo: si el teléfono estaba registrado con otra persona,
        // se reasigna a quien acaba de iniciar sesión.
        var existing = await _context.PushTokenDevices
            .FirstOrDefaultAsync(p => p.Token == request.Token);

        if (existing != null)
        {
            existing.UserId = userId;
            existing.Platform = request.Platform ?? "android";
            existing.ActualizadoEn = DateTime.UtcNow;
        }
        else
        {
            _context.PushTokenDevices.Add(new PushTokenDevice
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Token = request.Token,
                Platform = request.Platform ?? "android",
                ActualizadoEn = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
        return Ok(ApiResponseDto<object>.Ok(null!, "Token registrado."));
    }

    // DELETE /api/employee/push-token
    // Se llama al cerrar sesión. Idempotente, y solo borra el token del usuario del JWT.
    [HttpDelete("push-token")]
    public async Task<IActionResult> EliminarPushToken([FromBody] UnregisterPushTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest(ApiResponseDto<object>.Fail("El token es obligatorio."));

        var userId = User.UsuarioIdRequerido();

        await _context.PushTokenDevices
            .Where(p => p.UserId == userId && p.Token == request.Token)
            .ExecuteDeleteAsync();

        return Ok(ApiResponseDto<object>.Ok(null!, "Token eliminado."));
    }
}

public class UnregisterPushTokenRequest
{
    public string Token { get; set; } = string.Empty;
}

public class RegisterPushTokenRequest
{
    public string Token { get; set; } = string.Empty;
    public string? Platform { get; set; }
}
