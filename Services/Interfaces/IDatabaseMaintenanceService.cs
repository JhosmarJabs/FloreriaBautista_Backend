using FloreriaBautista.Models.DTOs.Database;

namespace FloreriaBautista.Services.Interfaces;

public interface IDatabaseMaintenanceService
{
    /// <summary>VACUUM ANALYZE en todas las tablas — libera espacio y actualiza estadísticas.</summary>
    Task<MaintenanceResponseDto> EjecutarVacuumAsync();

    /// <summary>REINDEX DATABASE — reconstruye todos los índices.</summary>
    Task<MaintenanceResponseDto> ReindexarAsync();

    /// <summary>Elimina registros de audit_logs, backup_jobs e import_jobs anteriores a N días.</summary>
    Task<MaintenanceResponseDto> LimpiarRegistrosAntiguosAsync(int diasAntiguedad = 90);

    /// <summary>Actualiza estadísticas del planner con ANALYZE.</summary>
    Task<MaintenanceResponseDto> ActualizarEstadisticasAsync();

    /// <summary>Ejecuta todas las tareas de mantenimiento en secuencia.</summary>
    Task<List<MaintenanceResponseDto>> EjecutarMantenimientoCompletoAsync();
}
