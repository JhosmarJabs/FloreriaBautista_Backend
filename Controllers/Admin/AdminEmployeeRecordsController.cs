using FloreriaBautista.Extensions;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Employee;
using FloreriaBautista.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FloreriaBautista.Controllers.Admin;

/// <summary>
/// La vista global de lo que registran los empleados: gastos, cortes de caja y
/// la bandeja de reportes de error. Es el otro lado de /api/employee — aquí sí
/// se acepta un rango de fechas y un id de empleado, porque el admin es quien
/// tiene permitido supervisar la operación completa.
/// </summary>
[ApiController]
[Tags("2. Operaciones y Ventas")]
[Route("api/admin")]
[Authorize(Roles = "ADMIN")]
public class AdminEmployeeRecordsController : ControllerBase
{
    private readonly IEmployeeExpenseService _expenses;
    private readonly ICashCutService         _cashCuts;
    private readonly IErrorReportService     _errorReports;

    public AdminEmployeeRecordsController(
        IEmployeeExpenseService expenses,
        ICashCutService         cashCuts,
        IErrorReportService     errorReports)
    {
        _expenses     = expenses;
        _cashCuts     = cashCuts;
        _errorReports = errorReports;
    }

    // GET /api/admin/expenses?usuarioId=&desde=2026-09-01&hasta=2026-09-15
    [HttpGet("expenses")]
    public async Task<IActionResult> Gastos(
        [FromQuery] Guid?     usuarioId,
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var resultado = await _expenses.ListarAdminAsync(usuarioId, desde, hasta, page, size);
        return Ok(ApiResponseDto<PagedResultDto<ExpenseDto>>.Ok(resultado));
    }

    // GET /api/admin/cash-cuts?usuarioId=&desde=&hasta=
    [HttpGet("cash-cuts")]
    public async Task<IActionResult> Cortes(
        [FromQuery] Guid?     usuarioId,
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var resultado = await _cashCuts.ListarAdminAsync(usuarioId, desde, hasta, page, size);
        return Ok(ApiResponseDto<PagedResultDto<CashCutDto>>.Ok(resultado));
    }

    // GET /api/admin/error-reports?estado=PENDIENTE
    [HttpGet("error-reports")]
    public async Task<IActionResult> Reportes(
        [FromQuery] string? estado,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var resultado = await _errorReports.ListarAdminAsync(estado, page, size);
        return Ok(ApiResponseDto<PagedResultDto<ErrorReportDto>>.Ok(resultado));
    }

    /// <summary>
    /// Resuelve un reporte. La acción y quién la tomó quedan en la bitácora de
    /// auditoría; los registros económicos se marcan ANULADO en vez de borrarse.
    /// </summary>
    // POST /api/admin/error-reports/{reporteId}/resolver
    [HttpPost("error-reports/{reporteId:guid}/resolver")]
    public async Task<IActionResult> ResolverReporte(
        Guid reporteId, [FromBody] ResolveErrorReportRequestDto request)
    {
        var reporte = await _errorReports.ResolverAsync(reporteId, User.UsuarioIdRequerido(), request);
        return Ok(ApiResponseDto<ErrorReportDto>.Ok(reporte, "Reporte resuelto."));
    }
}
