using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Reports;
using FloreriaBautista.Services.Interfaces;
using FloreriaBautista.Services.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FloreriaBautista.Controllers.Admin;

[ApiController]
[Tags("2. Operaciones y Ventas")]
[Route("api/admin/reports")]
[Authorize(Roles = "ADMIN")]
public class AdminReportsController : ControllerBase
{
    private readonly ReportsService          _reportsService;
    private readonly InventoryReportsService _inventoryReports;
    private readonly SalesReportsService     _salesReports;
    private readonly PeopleReportsService    _peopleReports;
    private readonly IFechaHelper            _fechas;

    public AdminReportsController(
        ReportsService          reportsService,
        InventoryReportsService inventoryReports,
        SalesReportsService     salesReports,
        PeopleReportsService    peopleReports,
        IFechaHelper            fechas)
    {
        _reportsService   = reportsService;
        _inventoryReports = inventoryReports;
        _salesReports     = salesReports;
        _peopleReports    = peopleReports;
        _fechas           = fechas;
    }

    // GET /api/admin/reports/sales?desde=2026-01-01&hasta=2026-03-31
    [HttpGet("sales")]
    public async Task<IActionResult> Ventas(
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta)
    {
        // Rango por defecto en fechas de la tienda, no en UTC (ver IFechaHelper).
        var hoy = _fechas.HoyLocal();
        var d   = desde ?? hoy.AddDays(-30);
        var h   = hasta ?? hoy;
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

    // GET /api/admin/reports/dashboard
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var stats = await _reportsService.ObtenerDashboardStatsAsync();
        return Ok(ApiResponseDto<DashboardStatsDto>.Ok(stats));
    }

    // ── Inventario ────────────────────────────────────────────────────────

    // GET /reports/inventory/movements?desde&hasta&granularidad&itemId&tipo&usuarioId
    [HttpGet("inventory/movements")]
    public async Task<IActionResult> Movimientos(
        [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta,
        [FromQuery] string? granularidad,
        [FromQuery] Guid? itemId, [FromQuery] string? tipo, [FromQuery] Guid? usuarioId)
    {
        var p = ReportPeriod.Resolver(desde, hasta);
        var g = ReportGranularityExtensions.Parse(granularidad);
        var reporte = await _inventoryReports.MovimientosAsync(p, g, itemId, tipo, usuarioId);
        return Ok(ApiResponseDto<InventoryMovementsReportDto>.Ok(reporte));
    }

    // GET /reports/inventory/waste?desde&hasta&granularidad  (merma por AJUSTE)
    [HttpGet("inventory/waste")]
    public async Task<IActionResult> Merma(
        [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta,
        [FromQuery] string? granularidad)
    {
        var p = ReportPeriod.Resolver(desde, hasta);
        var g = ReportGranularityExtensions.Parse(granularidad);
        var reporte = await _inventoryReports.MermaAsync(p, g);
        return Ok(ApiResponseDto<WasteReportDto>.Ok(reporte));
    }

    // GET /reports/inventory/dead-stock?dias=90
    [HttpGet("inventory/dead-stock")]
    public async Task<IActionResult> StockMuerto([FromQuery] int dias = 90)
    {
        var reporte = await _inventoryReports.StockMuertoAsync(dias);
        return Ok(ApiResponseDto<DeadStockReportDto>.Ok(reporte));
    }

    // ── Ventas ────────────────────────────────────────────────────────────

    // GET /reports/sales/overview?desde&hasta&granularidad
    [HttpGet("sales/overview")]
    public async Task<IActionResult> PanoramaVentas(
        [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta,
        [FromQuery] string? granularidad)
    {
        var p = ReportPeriod.Resolver(desde, hasta);
        var g = ReportGranularityExtensions.Parse(granularidad);
        var reporte = await _salesReports.PanoramaAsync(p, g);
        return Ok(ApiResponseDto<SalesOverviewDto>.Ok(reporte));
    }

    // GET /reports/sales/profitability?desde&hasta  (margen por producto)
    [HttpGet("sales/profitability")]
    public async Task<IActionResult> Rentabilidad(
        [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta)
    {
        var p = ReportPeriod.Resolver(desde, hasta);
        var reporte = await _salesReports.RentabilidadAsync(p);
        return Ok(ApiResponseDto<ProfitabilityReportDto>.Ok(reporte));
    }

    // GET /reports/sales/seasonality?anios=2  (estacionalidad por festividad)
    [HttpGet("sales/seasonality")]
    public async Task<IActionResult> Estacionalidad([FromQuery] int anios = 2)
    {
        var reporte = await _salesReports.EstacionalidadAsync(anios);
        return Ok(ApiResponseDto<SeasonalityReportDto>.Ok(reporte));
    }

    // ── Personas ──────────────────────────────────────────────────────────

    // GET /reports/staff/performance?desde&hasta  (desempeño de empleado)
    [HttpGet("staff/performance")]
    public async Task<IActionResult> DesempenoEmpleado(
        [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta)
    {
        var p = ReportPeriod.Resolver(desde, hasta);
        var reporte = await _peopleReports.DesempenoAsync(p);
        return Ok(ApiResponseDto<StaffPerformanceReportDto>.Ok(reporte));
    }

    // GET /reports/deliveries?desde&hasta&granularidad  (cumplimiento de entregas)
    [HttpGet("deliveries")]
    public async Task<IActionResult> Entregas(
        [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta,
        [FromQuery] string? granularidad)
    {
        var p = ReportPeriod.Resolver(desde, hasta);
        var g = ReportGranularityExtensions.Parse(granularidad);
        var reporte = await _peopleReports.EntregasAsync(p, g);
        return Ok(ApiResponseDto<DeliveryFulfillmentReportDto>.Ok(reporte));
    }

    // GET /reports/customers?desde&hasta&granularidad  (nuevos vs recurrentes)
    [HttpGet("customers")]
    public async Task<IActionResult> Clientes(
        [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta,
        [FromQuery] string? granularidad)
    {
        var p = ReportPeriod.Resolver(desde, hasta);
        var g = ReportGranularityExtensions.Parse(granularidad);
        var reporte = await _peopleReports.ClientesAsync(p, g);
        return Ok(ApiResponseDto<CustomerRetentionReportDto>.Ok(reporte));
    }

    // GET /reports/receivables?desde&hasta  (cuentas por cobrar)
    [HttpGet("receivables")]
    public async Task<IActionResult> CuentasPorCobrar(
        [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta)
    {
        var p = ReportPeriod.Resolver(desde, hasta);
        var reporte = await _peopleReports.CuentasPorCobrarAsync(p);
        return Ok(ApiResponseDto<ReceivablesReportDto>.Ok(reporte));
    }
}
