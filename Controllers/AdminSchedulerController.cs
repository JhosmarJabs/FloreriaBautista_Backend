using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Scheduler;
using FloreriaBautista.Services.Scheduler;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FloreriaBautista.Controllers;

[ApiController]
[Route("api/admin/scheduler")]
[Authorize(Roles = "ADMIN")]
public class AdminSchedulerController : ControllerBase
{
    private readonly BackupSchedulerService _scheduler;

    public AdminSchedulerController(BackupSchedulerService scheduler)
        => _scheduler = scheduler;

    // GET /api/admin/scheduler — ver configuración actual
    [HttpGet]
    public IActionResult ObtenerConfig()
    {
        var cfg = _scheduler.Config;
        return Ok(ApiResponseDto<SchedulerConfigDto>.Ok(new SchedulerConfigDto
        {
            BackupAutomaticoActivo = cfg.BackupAutomaticoActivo,
            Frecuencia             = cfg.Frecuencia,
            DiaSemana              = cfg.DiaSemana,
            NombreDia              = cfg.NombreDia,
            Hora                   = cfg.Hora,
            HoraFormato            = cfg.HoraFormato,
            MantenimientoActivo    = cfg.MantenimientoActivo,
            ProximoBackup          = cfg.ProximaEjecucion(false),
            ProximoMantenimiento   = cfg.ProximaEjecucion(true)
        }));
    }

    // POST /api/admin/scheduler — actualizar configuración en tiempo real
    [HttpPost]
    public IActionResult ActualizarConfig([FromBody] UpdateSchedulerConfigDto request)
    {
        var cfg = _scheduler.Config;

        if (request.BackupAutomaticoActivo.HasValue)
            cfg.BackupAutomaticoActivo = request.BackupAutomaticoActivo.Value;

        if (!string.IsNullOrWhiteSpace(request.Frecuencia))
        {
            var freq = request.Frecuencia.ToUpper();
            if (freq != "DIARIO" && freq != "SEMANAL")
                return BadRequest(ApiResponseDto<object>.Fail("Frecuencia inválida. Use: DIARIO o SEMANAL"));
            cfg.Frecuencia = freq;
        }

        if (request.DiaSemana.HasValue)
        {
            if (request.DiaSemana < 0 || request.DiaSemana > 6)
                return BadRequest(ApiResponseDto<object>.Fail("DiaSemana debe ser entre 0 (Domingo) y 6 (Sábado)"));
            cfg.DiaSemana = request.DiaSemana.Value;
        }

        if (request.Hora.HasValue)
        {
            if (request.Hora < 0 || request.Hora > 23)
                return BadRequest(ApiResponseDto<object>.Fail("Hora debe ser entre 0 y 23"));
            cfg.Hora = request.Hora.Value;
        }

        if (request.MantenimientoActivo.HasValue)
            cfg.MantenimientoActivo = request.MantenimientoActivo.Value;

        return Ok(ApiResponseDto<SchedulerConfigDto>.Ok(new SchedulerConfigDto
        {
            BackupAutomaticoActivo = cfg.BackupAutomaticoActivo,
            Frecuencia             = cfg.Frecuencia,
            DiaSemana              = cfg.DiaSemana,
            NombreDia              = cfg.NombreDia,
            Hora                   = cfg.Hora,
            HoraFormato            = cfg.HoraFormato,
            MantenimientoActivo    = cfg.MantenimientoActivo,
            ProximoBackup          = cfg.ProximaEjecucion(false),
            ProximoMantenimiento   = cfg.ProximaEjecucion(true)
        }, "Configuración actualizada. Los cambios aplican de inmediato."));
    }
}
