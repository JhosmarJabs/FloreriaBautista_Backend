using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Reports;
using FloreriaBautista.Services.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FloreriaBautista.Controllers;

[ApiController]
[Route("api/admin/reports")]
[Authorize(Roles = "ADMIN")]
public class AdminReportsController : ControllerBase
{
    private readonly ReportsService _reportsService;
    public AdminReportsController(ReportsService reportsService)
        => _reportsService = reportsService;

    // GET /api/admin/reports/sales?desde=2026-01-01&hasta=2026-03-31
    [HttpGet("sales")]
    public async Task<IActionResult> Ventas(
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta)
    {
        var d = desde ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var h = hasta ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var reporte = await _reportsService.ReporteVentasAsync(d, h);
        return Ok(ApiResponseDto<SalesReportDto>.Ok(reporte));
    }

    // GET /api/admin/reports/top-products?top=10
    [HttpGet("top-products")]
    public async Task<IActionResult> TopProductos(
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        [FromQuery] int top = 10)
    {
        var resultado = await _reportsService.TopProductosAsync(desde, hasta, top);
        return Ok(ApiResponseDto<List<TopProductDto>>.Ok(resultado));
    }

    // GET /api/admin/reports/top-customers?top=10
    [HttpGet("top-customers")]
    public async Task<IActionResult> TopClientes(
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        [FromQuery] int top = 10)
    {
        var resultado = await _reportsService.TopClientesAsync(desde, hasta, top);
        return Ok(ApiResponseDto<List<TopCustomerDto>>.Ok(resultado));
    }

    // GET /api/admin/reports/inventory
    [HttpGet("inventory")]
    public async Task<IActionResult> Inventario()
    {
        var reporte = await _reportsService.ReporteInventarioAsync();
        return Ok(ApiResponseDto<InventoryReportDto>.Ok(reporte));
    }
}
