using FloreriaBautista.Services.Interfaces;

namespace FloreriaBautista.Services.Scheduler;

/// <summary>
/// Servicio en background que ejecuta tareas programadas automáticamente.
///
/// Calendario semanal (configurable en .env):
///   - Domingo medianoche → Backup FULL + Mantenimiento completo
///   - Cada hora          → Verificación de salud de la BD (log)
///
/// BACKUP_DIA_SEMANA: 0=Domingo, 1=Lunes ... 6=Sábado
/// BACKUP_HORA: hora en formato 24h (0-23)
/// </summary>
public class BackupSchedulerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackupSchedulerService> _logger;

    // Intervalo del loop principal: revisa cada minuto si hay tarea pendiente
    private static readonly TimeSpan IntervaloRevision = TimeSpan.FromMinutes(1);

    public BackupSchedulerService(
        IServiceScopeFactory scopeFactory,
        ILogger<BackupSchedulerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BackupSchedulerService iniciado.");

        DateTime? ultimoBackup      = null;
        DateTime? ultimoMantenimiento = null;
        DateTime? ultimaVerificacion  = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            var ahora = DateTime.UtcNow;

            // ── Backup semanal automático ──────────────────────────
            if (EsHoraDeBackup(ahora, ultimoBackup))
            {
                _logger.LogInformation("Iniciando backup semanal automático...");
                await EjecutarBackupAsync();
                ultimoBackup = ahora;
            }

            // ── Mantenimiento semanal (domingo a las 01:00) ────────
            if (EsHoraDeMantenimiento(ahora, ultimoMantenimiento))
            {
                _logger.LogInformation("Iniciando mantenimiento semanal automático...");
                await EjecutarMantenimientoAsync();
                ultimoMantenimiento = ahora;
            }

            // ── Health check cada hora ─────────────────────────────
            if (ultimaVerificacion is null || (ahora - ultimaVerificacion.Value).TotalHours >= 1)
            {
                await VerificarSaludAsync();
                ultimaVerificacion = ahora;
            }

            await Task.Delay(IntervaloRevision, stoppingToken);
        }

        _logger.LogInformation("BackupSchedulerService detenido.");
    }

    // ── Ejecuciones ───────────────────────────────────────────────

    private async Task EjecutarBackupAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();

        try
        {
            // GUID vacío: backup automático sin usuario específico
            var resultado = await backupService.CrearBackupFullAsync(
                "Backup semanal automático", Guid.Empty);

            _logger.LogInformation(
                "Backup automático {Estado}. Drive: {Subido}. Archivo: {Archivo}",
                resultado.Estado,
                resultado.SubidoADrive ? "✔" : "✘",
                resultado.RutaArchivoLocal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en backup automático semanal");
        }
    }

    private async Task EjecutarMantenimientoAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var maintenance = scope.ServiceProvider.GetRequiredService<IDatabaseMaintenanceService>();

        try
        {
            var resultados = await maintenance.EjecutarMantenimientoCompletoAsync();
            foreach (var r in resultados)
                _logger.LogInformation("Mantenimiento [{Tarea}]: {Estado} ({Ms:F0} ms)",
                    r.Tarea, r.Estado, r.DuracionMs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en mantenimiento automático semanal");
        }
    }

    private async Task VerificarSaludAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var health = scope.ServiceProvider.GetRequiredService<IDatabaseHealthService>();

        try
        {
            var resultado = await health.VerificarConexionAsync();
            if (resultado.Conectado)
                _logger.LogInformation(
                    "Health check BD: OK | Conexiones: {Activas}/{Max} | Respuesta: {Ms}",
                    resultado.ConexionesActivas, resultado.ConexionesMaximas, resultado.TiempoRespuesta);
            else
                _logger.LogWarning("Health check BD: ERROR — {Error}", resultado.MensajeError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en health check automático");
        }
    }

    // ── Lógica de calendario ──────────────────────────────────────

    private static bool EsHoraDeBackup(DateTime ahora, DateTime? ultimaEjecucion)
    {
        var diaObj = int.TryParse(
            Environment.GetEnvironmentVariable("BACKUP_DIA_SEMANA"), out var d) ? d : 0; // Domingo
        var hora = int.TryParse(
            Environment.GetEnvironmentVariable("BACKUP_HORA"), out var h) ? h : 0;       // Medianoche

        var esDiaCorrecto  = (int)ahora.DayOfWeek == diaObj;
        var esHoraCorrecto = ahora.Hour == hora && ahora.Minute < 5; // ventana de 5 min

        if (!esDiaCorrecto || !esHoraCorrecto) return false;

        // Evitar doble ejecución en la misma ventana
        if (ultimaEjecucion is null) return true;
        return (ahora - ultimaEjecucion.Value).TotalHours > 23;
    }

    private static bool EsHoraDeMantenimiento(DateTime ahora, DateTime? ultimaEjecucion)
    {
        // Mantenimiento: mismo día del backup, 1 hora después
        var diaObj = int.TryParse(
            Environment.GetEnvironmentVariable("BACKUP_DIA_SEMANA"), out var d) ? d : 0;
        var hora = int.TryParse(
            Environment.GetEnvironmentVariable("BACKUP_HORA"), out var h) ? h + 1 : 1;

        var esDiaCorrecto  = (int)ahora.DayOfWeek == diaObj;
        var esHoraCorrecto = ahora.Hour == hora && ahora.Minute < 5;

        if (!esDiaCorrecto || !esHoraCorrecto) return false;
        if (ultimaEjecucion is null) return true;
        return (ahora - ultimaEjecucion.Value).TotalHours > 23;
    }
}
