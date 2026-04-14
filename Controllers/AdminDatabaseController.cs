using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Database;
using FloreriaBautista.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FloreriaBautista.Controllers;

[ApiController]
[Tags("Administrador")]
[Route("api/admin/database")]
[Authorize(Roles = "ADMIN")]
public class AdminDatabaseController : ControllerBase
{
    private readonly IDatabaseHealthService      _health;
    private readonly IDatabaseMonitorService     _monitor;
    private readonly IDatabaseMaintenanceService _maintenance;
    private readonly IRestoreService             _restore;

    public AdminDatabaseController(
        IDatabaseHealthService      health,
        IDatabaseMonitorService     monitor,
        IDatabaseMaintenanceService maintenance,
        IRestoreService             restore)
    {
        _health      = health;
        _monitor     = monitor;
        _maintenance = maintenance;
        _restore     = restore;
    }

    // GET /api/admin/database/health
    [HttpGet("health")]
    public async Task<IActionResult> Health()
    {
        var resultado = await _health.VerificarConexionAsync();
        var status    = resultado.Conectado ? 200 : 503;
        return StatusCode(status, ApiResponseDto<HealthResponseDto>.Ok(resultado));
    }

    // GET /api/admin/database/monitor
    [HttpGet("monitor")]
    public async Task<IActionResult> Monitor()
    {
        var reporte = await _monitor.GenerarReporteCompletoAsync();
        return Ok(ApiResponseDto<MonitorReportDto>.Ok(reporte));
    }

    // POST /api/admin/database/mantenimiento
    // Ejecuta limpieza + analyze + vacuum + reindex en secuencia
    [HttpPost("mantenimiento")]
    public async Task<IActionResult> MantenimientoCompleto()
    {
        var resultados = await _maintenance.EjecutarMantenimientoCompletoAsync();
        return Ok(ApiResponseDto<List<MaintenanceResponseDto>>.Ok(
            resultados, "Mantenimiento completo ejecutado."));
    }

    // POST /api/admin/database/restaurar
    [HttpPost("restaurar")]
    public async Task<IActionResult> Restaurar([FromBody] RestoreRequestDto request)
    {
        var resultado = await _restore.RestaurarDesdeDriveAsync(request);
        var status    = resultado.Estado == "COMPLETADO" ? 200 : 422;
        return StatusCode(status, ApiResponseDto<RestoreResponseDto>.Ok(resultado));
    }
}
