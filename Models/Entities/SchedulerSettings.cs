namespace FloreriaBautista.Models.Entities;

public class SchedulerSettings
{
    public int      Id                          { get; set; } = 1;

    // ── Backup ────────────────────────────────────────────────────
    public bool     BackupAutomaticoActivo      { get; set; } = true;
    public string   Frecuencia                  { get; set; } = "SEMANAL";
    public int      DiaSemana                   { get; set; } = 0;
    public int      Hora                        { get; set; } = 0;

    // ── Mantenimiento ─────────────────────────────────────────────
    public bool     MantenimientoActivo         { get; set; } = true;
    public string   FrecuenciaMantenimiento     { get; set; } = "SEMANAL";
    public int      DiaSemanaMantenimiento      { get; set; } = 0;
    public int      HoraMantenimiento           { get; set; } = 1;

    public DateTime ActualizadoEn               { get; set; } = DateTime.UtcNow;
}
