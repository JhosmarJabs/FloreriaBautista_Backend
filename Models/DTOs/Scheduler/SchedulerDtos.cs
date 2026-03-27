namespace FloreriaBautista.Models.DTOs.Scheduler;

public class SchedulerConfigDto
{
    // ── Backup ────────────────────────────────────────────────────
    public bool   BackupAutomaticoActivo      { get; set; }
    public string Frecuencia                  { get; set; } = string.Empty;
    public int    DiaSemana                   { get; set; }
    public string NombreDia                   { get; set; } = string.Empty;
    public int    Hora                        { get; set; }
    public string HoraFormato                 { get; set; } = string.Empty;
    public string ProximoBackup               { get; set; } = string.Empty;

    // ── Mantenimiento ─────────────────────────────────────────────
    public bool   MantenimientoActivo         { get; set; }
    public string FrecuenciaMantenimiento     { get; set; } = string.Empty;
    public int    DiaSemanaMantenimiento      { get; set; }
    public string NombreDiaMantenimiento      { get; set; } = string.Empty;
    public int    HoraMantenimiento           { get; set; }
    public string HoraMantenimientoFormato    { get; set; } = string.Empty;
    public string ProximoMantenimiento        { get; set; } = string.Empty;
}

public class UpdateSchedulerConfigDto
{
    // ── Backup ────────────────────────────────────────────────────
    public bool?   BackupAutomaticoActivo      { get; set; }
    public string? Frecuencia                  { get; set; } // DIARIO | SEMANAL
    public int?    DiaSemana                   { get; set; } // 0=Dom ... 6=Sáb
    public int?    Hora                        { get; set; } // 0-23

    // ── Mantenimiento ─────────────────────────────────────────────
    public bool?   MantenimientoActivo         { get; set; }
    public string? FrecuenciaMantenimiento     { get; set; } // DIARIO | SEMANAL
    public int?    DiaSemanaMantenimiento      { get; set; } // 0=Dom ... 6=Sáb
    public int?    HoraMantenimiento           { get; set; } // 0-23
}
