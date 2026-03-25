namespace FloreriaBautista.Models.Entities;

/// <summary>
/// Fila singleton (Id = 1) que persiste la configuración del scheduler en BD.
/// Se carga al arrancar el servicio y se actualiza en cada POST /api/admin/scheduler.
/// </summary>
public class SchedulerSettings
{
    public int      Id                     { get; set; } = 1;
    public bool     BackupAutomaticoActivo { get; set; } = true;
    public string   Frecuencia             { get; set; } = "SEMANAL";
    public int      DiaSemana              { get; set; } = 0;
    public int      Hora                   { get; set; } = 0;
    public bool     MantenimientoActivo    { get; set; } = true;
    public DateTime ActualizadoEn          { get; set; } = DateTime.UtcNow;
}
