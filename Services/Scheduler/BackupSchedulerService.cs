using FloreriaBautista.Data;
using FloreriaBautista.Services.Interfaces;

namespace FloreriaBautista.Services.Scheduler;

public class BackupSchedulerService : BackgroundService
{
    private readonly IServiceScopeFactory            _scopeFactory;
    private readonly ILogger<BackupSchedulerService> _logger;
    public  readonly SchedulerConfig                 Config;

    private static readonly TimeSpan IntervaloRevision = TimeSpan.FromMinutes(1);

    public BackupSchedulerService(
        IServiceScopeFactory scopeFactory,
        ILogger<BackupSchedulerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
        Config        = SchedulerConfig.CargarDesdeEnv();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CargarDesdeDbAsync();

        _logger.LogInformation("BackupSchedulerService iniciado. Config: {Freq} | Día: {Dia} | Hora: {Hora}",
            Config.Frecuencia, Config.NombreDia, Config.HoraFormato);

        DateTime? ultimoBackup        = null;
        DateTime? ultimoMantenimiento = null;
        DateTime? ultimaVerificacion  = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            var ahora = DateTime.UtcNow;

            if (Config.BackupAutomaticoActivo && EsHoraDeEjecucion(ahora, ultimoBackup, false))
            {
                _logger.LogInformation("Iniciando backup automático ({Freq})...", Config.Frecuencia);
                await EjecutarBackupAsync();
                ultimoBackup = ahora;
            }

            if (Config.MantenimientoActivo && EsHoraDeEjecucion(ahora, ultimoMantenimiento, true))
            {
                _logger.LogInformation("Iniciando mantenimiento automático...");
                await EjecutarMantenimientoAsync();
                ultimoMantenimiento = ahora;
            }

            if (ultimaVerificacion is null || (ahora - ultimaVerificacion.Value).TotalHours >= 1)
            {
                await VerificarSaludAsync();
                ultimaVerificacion = ahora;
            }

            await Task.Delay(IntervaloRevision, stoppingToken);
        }

        _logger.LogInformation("BackupSchedulerService detenido.");
    }

    private async Task EjecutarBackupAsync()
    {
        using var scope         = _scopeFactory.CreateScope();
        var       backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();
        var       audit         = scope.ServiceProvider.GetRequiredService<IAuditService>();
        try
        {
            var resultado = await backupService.CrearBackupFullAsync("Backup automático", Guid.Empty);
            _logger.LogInformation("Backup automático {Estado}. Drive: {Subido}",
                resultado.Estado, resultado.SubidoADrive ? "✔" : "✘");

            await audit.RegistrarAsync("BACKUP_AUTOMATICO", "BackupJob", resultado.Id.ToString(), null,
                new { resultado.RutaArchivoLocal, resultado.Estado, resultado.SubidoADrive, Config.Frecuencia });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error en backup automático"); }
    }

    private async Task EjecutarMantenimientoAsync()
    {
        using var scope       = _scopeFactory.CreateScope();
        var       maintenance = scope.ServiceProvider.GetRequiredService<IDatabaseMaintenanceService>();
        try
        {
            var resultados = await maintenance.EjecutarMantenimientoCompletoAsync();
            foreach (var r in resultados)
                _logger.LogInformation("Mantenimiento [{Tarea}]: {Estado}", r.Tarea, r.Estado);
        }
        catch (Exception ex) { _logger.LogError(ex, "Error en mantenimiento automático"); }
    }

    private async Task VerificarSaludAsync()
    {
        using var scope  = _scopeFactory.CreateScope();
        var       health = scope.ServiceProvider.GetRequiredService<IDatabaseHealthService>();
        try
        {
            var resultado = await health.VerificarConexionAsync();
            if (resultado.Conectado)
                _logger.LogInformation("Health check BD: OK | Conexiones: {A}/{M}",
                    resultado.ConexionesActivas, resultado.ConexionesMaximas);
            else
                _logger.LogWarning("Health check BD: ERROR — {Error}", resultado.MensajeError);
        }
        catch (Exception ex) { _logger.LogError(ex, "Error en health check"); }
    }

    private async Task CargarDesdeDbAsync()
    {
        try
        {
            using var scope    = _scopeFactory.CreateScope();
            var       context  = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var       settings = await context.SchedulerSettings.FindAsync(1);
            if (settings != null)
            {
                Config.BackupAutomaticoActivo      = settings.BackupAutomaticoActivo;
                Config.Frecuencia                  = settings.Frecuencia;
                Config.DiaSemana                   = settings.DiaSemana;
                Config.Hora                        = settings.Hora;
                Config.MantenimientoActivo         = settings.MantenimientoActivo;
                Config.FrecuenciaMantenimiento     = settings.FrecuenciaMantenimiento;
                Config.DiaSemanaMantenimiento      = settings.DiaSemanaMantenimiento;
                Config.HoraMantenimiento           = settings.HoraMantenimiento;
                _logger.LogInformation(
                    "Scheduler config cargada desde BD — Backup: {Freq} Día {Dia} {Hora}:00 | Mant: {FreqM} Día {DiaM} {HoraM}:00",
                    settings.Frecuencia, settings.DiaSemana, settings.Hora,
                    settings.FrecuenciaMantenimiento, settings.DiaSemanaMantenimiento, settings.HoraMantenimiento);
            }
            else
            {
                _logger.LogWarning("No se encontró scheduler_settings en BD. Usando valores de variables de entorno.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo cargar scheduler config desde BD. Usando valores por defecto.");
        }
    }

    private bool EsHoraDeEjecucion(DateTime ahora, DateTime? ultimaEjecucion, bool esMantenimiento)
    {
        var hora      = esMantenimiento ? Config.HoraMantenimiento       : Config.Hora;
        var diaSemana = esMantenimiento ? Config.DiaSemanaMantenimiento  : Config.DiaSemana;
        var frecuencia = esMantenimiento ? Config.FrecuenciaMantenimiento : Config.Frecuencia;

        bool esHora;
        if (frecuencia == "DIARIO")
        {
            esHora = ahora.Hour == hora && ahora.Minute < 5;
        }
        else // SEMANAL
        {
            var esDia = (int)ahora.DayOfWeek == diaSemana;
            esHora    = esDia && ahora.Hour == hora && ahora.Minute < 5;
        }

        if (!esHora) return false;
        if (ultimaEjecucion is null) return true;

        var horasMinimas = frecuencia == "DIARIO" ? 23.0 : 167.0; // ~7 días
        return (ahora - ultimaEjecucion.Value).TotalHours > horasMinimas;
    }
}
