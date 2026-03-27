using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Scheduler;
using FloreriaBautista.Models.Entities;
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
    private readonly AppDbContext           _context;

    public AdminSchedulerController(BackupSchedulerService scheduler, AppDbContext context)
    {
        _scheduler = scheduler;
        _context   = context;
    }

    // GET /api/admin/scheduler
    [HttpGet]
    public IActionResult ObtenerConfig()
    {
        var cfg = _scheduler.Config;
        return Ok(ApiResponseDto<SchedulerConfigDto>.Ok(new SchedulerConfigDto
        {
            // Backup
            BackupAutomaticoActivo      = cfg.BackupAutomaticoActivo,
            Frecuencia                  = cfg.Frecuencia,
            DiaSemana                   = cfg.DiaSemana,
            NombreDia                   = cfg.NombreDia,
            Hora                        = cfg.Hora,
            HoraFormato                 = cfg.HoraFormato,
            ProximoBackup               = cfg.ProximaEjecucion(false),
            // Mantenimiento
            MantenimientoActivo         = cfg.MantenimientoActivo,
            FrecuenciaMantenimiento     = cfg.FrecuenciaMantenimiento,
            DiaSemanaMantenimiento      = cfg.DiaSemanaMantenimiento,
            NombreDiaMantenimiento      = cfg.NombreDiaMantenimiento,
            HoraMantenimiento           = cfg.HoraMantenimiento,
            HoraMantenimientoFormato    = cfg.HoraMantenimientoFormato,
            ProximoMantenimiento        = cfg.ProximaEjecucion(true)
        }));
    }

    // POST /api/admin/scheduler
    [HttpPost]
    public async Task<IActionResult> ActualizarConfig([FromBody] UpdateSchedulerConfigDto request)
    {
        var cfg = _scheduler.Config;

        // ── Backup ──────────────────────────────────────────────────
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

        // ── Mantenimiento ───────────────────────────────────────────
        if (request.MantenimientoActivo.HasValue)
            cfg.MantenimientoActivo = request.MantenimientoActivo.Value;

        if (!string.IsNullOrWhiteSpace(request.FrecuenciaMantenimiento))
        {
            var freq = request.FrecuenciaMantenimiento.ToUpper();
            if (freq != "DIARIO" && freq != "SEMANAL")
                return BadRequest(ApiResponseDto<object>.Fail("FrecuenciaMantenimiento inválida. Use: DIARIO o SEMANAL"));
            cfg.FrecuenciaMantenimiento = freq;
        }

        if (request.DiaSemanaMantenimiento.HasValue)
        {
            if (request.DiaSemanaMantenimiento < 0 || request.DiaSemanaMantenimiento > 6)
                return BadRequest(ApiResponseDto<object>.Fail("DiaSemanaMantenimiento debe ser entre 0 (Domingo) y 6 (Sábado)"));
            cfg.DiaSemanaMantenimiento = request.DiaSemanaMantenimiento.Value;
        }

        if (request.HoraMantenimiento.HasValue)
        {
            if (request.HoraMantenimiento < 0 || request.HoraMantenimiento > 23)
                return BadRequest(ApiResponseDto<object>.Fail("HoraMantenimiento debe ser entre 0 y 23"));
            cfg.HoraMantenimiento = request.HoraMantenimiento.Value;
        }

        // ── Persistir en BD (upsert singleton id=1) ─────────────────
        var settings = await _context.SchedulerSettings.FindAsync(1);
        if (settings is null)
        {
            _context.SchedulerSettings.Add(new SchedulerSettings
            {
                Id                          = 1,
                BackupAutomaticoActivo      = cfg.BackupAutomaticoActivo,
                Frecuencia                  = cfg.Frecuencia,
                DiaSemana                   = cfg.DiaSemana,
                Hora                        = cfg.Hora,
                MantenimientoActivo         = cfg.MantenimientoActivo,
                FrecuenciaMantenimiento     = cfg.FrecuenciaMantenimiento,
                DiaSemanaMantenimiento      = cfg.DiaSemanaMantenimiento,
                HoraMantenimiento           = cfg.HoraMantenimiento,
                ActualizadoEn               = DateTime.UtcNow
            });
        }
        else
        {
            settings.BackupAutomaticoActivo      = cfg.BackupAutomaticoActivo;
            settings.Frecuencia                  = cfg.Frecuencia;
            settings.DiaSemana                   = cfg.DiaSemana;
            settings.Hora                        = cfg.Hora;
            settings.MantenimientoActivo         = cfg.MantenimientoActivo;
            settings.FrecuenciaMantenimiento     = cfg.FrecuenciaMantenimiento;
            settings.DiaSemanaMantenimiento      = cfg.DiaSemanaMantenimiento;
            settings.HoraMantenimiento           = cfg.HoraMantenimiento;
            settings.ActualizadoEn               = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync();

        return Ok(ApiResponseDto<SchedulerConfigDto>.Ok(new SchedulerConfigDto
        {
            BackupAutomaticoActivo      = cfg.BackupAutomaticoActivo,
            Frecuencia                  = cfg.Frecuencia,
            DiaSemana                   = cfg.DiaSemana,
            NombreDia                   = cfg.NombreDia,
            Hora                        = cfg.Hora,
            HoraFormato                 = cfg.HoraFormato,
            ProximoBackup               = cfg.ProximaEjecucion(false),
            MantenimientoActivo         = cfg.MantenimientoActivo,
            FrecuenciaMantenimiento     = cfg.FrecuenciaMantenimiento,
            DiaSemanaMantenimiento      = cfg.DiaSemanaMantenimiento,
            NombreDiaMantenimiento      = cfg.NombreDiaMantenimiento,
            HoraMantenimiento           = cfg.HoraMantenimiento,
            HoraMantenimientoFormato    = cfg.HoraMantenimientoFormato,
            ProximoMantenimiento        = cfg.ProximaEjecucion(true)
        }, "Configuración actualizada. Los cambios aplican de inmediato."));
    }
}
