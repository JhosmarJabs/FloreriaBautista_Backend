namespace FloreriaBautista.Models.DTOs.Scheduler;

public class SchedulerConfigDto
{
    public bool   BackupAutomaticoActivo { get; set; }
    public string Frecuencia             { get; set; } = string.Empty; // DIARIO, SEMANAL
    public int    DiaSemana              { get; set; }
    public string NombreDia              { get; set; } = string.Empty;
    public int    Hora                   { get; set; }
    public string HoraFormato            { get; set; } = string.Empty;
    public bool   MantenimientoActivo    { get; set; }
    public string ProximoBackup          { get; set; } = string.Empty;
    public string ProximoMantenimiento   { get; set; } = string.Empty;
}

public class UpdateSchedulerConfigDto
{
    public bool?   BackupAutomaticoActivo { get; set; }
    public string? Frecuencia             { get; set; } // DIARIO, SEMANAL
    public int?    DiaSemana              { get; set; } // 0=Dom ... 6=Sáb
    public int?    Hora                   { get; set; } // 0-23
    public bool?   MantenimientoActivo    { get; set; }
}
