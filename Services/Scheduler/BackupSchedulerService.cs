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
        try
        {
            var resultado = await backupService.CrearBackupFullAsync("Backup automático", Guid.Empty);
            _logger.LogInformation("Backup automático {Estado}. Drive: {Subido}",
                resultado.Estado, resultado.SubidoADrive ? "✔" : "✘");
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

    private bool EsHoraDeEjecucion(DateTime ahora, DateTime? ultimaEjecucion, bool esMantenimiento)
    {
        var hora = esMantenimiento ? Config.Hora + 1 : Config.Hora;

        bool esHora;
        if (Config.Frecuencia == "DIARIO")
        {
            esHora = ahora.Hour == hora && ahora.Minute < 5;
        }
        else // SEMANAL
        {
            var esDia = (int)ahora.DayOfWeek == Config.DiaSemana;
            esHora    = esDia && ahora.Hour == hora && ahora.Minute < 5;
        }

        if (!esHora) return false;
        if (ultimaEjecucion is null) return true;

        var horasMinimas = Config.Frecuencia == "DIARIO" ? 23.0 : 167.0; // ~7 días
        return (ahora - ultimaEjecucion.Value).TotalHours > horasMinimas;
    }
}
