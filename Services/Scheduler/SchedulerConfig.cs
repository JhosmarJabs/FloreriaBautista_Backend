namespace FloreriaBautista.Services.Scheduler;

public class SchedulerConfig
{
    // ── Backup ────────────────────────────────────────────────────
    public bool   BackupAutomaticoActivo  { get; set; }
    public string Frecuencia             { get; set; } = "SEMANAL";
    public int    DiaSemana              { get; set; }
    public int    Hora                   { get; set; }

    // ── Mantenimiento ─────────────────────────────────────────────
    public bool   MantenimientoActivo         { get; set; } = true;
    public string FrecuenciaMantenimiento     { get; set; } = "SEMANAL";
    public int    DiaSemanaMantenimiento      { get; set; }
    public int    HoraMantenimiento           { get; set; } = 1;

    public static SchedulerConfig CargarDesdeEnv() => new()
    {
        BackupAutomaticoActivo      = true,
        Frecuencia                  = Environment.GetEnvironmentVariable("BACKUP_FRECUENCIA")             ?? "SEMANAL",
        DiaSemana                   = int.TryParse(Environment.GetEnvironmentVariable("BACKUP_DIA_SEMANA"),    out var d)  ? d  : 0,
        Hora                        = int.TryParse(Environment.GetEnvironmentVariable("BACKUP_HORA"),          out var h)  ? h  : 0,
        MantenimientoActivo         = true,
        FrecuenciaMantenimiento     = Environment.GetEnvironmentVariable("MANT_FRECUENCIA")               ?? "SEMANAL",
        DiaSemanaMantenimiento      = int.TryParse(Environment.GetEnvironmentVariable("MANT_DIA_SEMANA"),      out var dm) ? dm : 0,
        HoraMantenimiento           = int.TryParse(Environment.GetEnvironmentVariable("MANT_HORA"),            out var hm) ? hm : 1,
    };

    private static readonly string[] NombresDias =
        ["Domingo", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado"];

    public string NombreDia                => DiaSemana >= 0 && DiaSemana < 7               ? NombresDias[DiaSemana]               : "?";
    public string HoraFormato              => $"{Hora:D2}:00";
    public string NombreDiaMantenimiento   => DiaSemanaMantenimiento >= 0 && DiaSemanaMantenimiento < 7 ? NombresDias[DiaSemanaMantenimiento] : "?";
    public string HoraMantenimientoFormato => $"{HoraMantenimiento:D2}:00";

    public string ProximaEjecucion(bool esMantenimiento = false)
    {
        if (esMantenimiento && !MantenimientoActivo) return "Desactivado";
        if (!esMantenimiento && !BackupAutomaticoActivo) return "Desactivado";

        var ahora     = DateTime.UtcNow;
        var frecuencia = esMantenimiento ? FrecuenciaMantenimiento : Frecuencia;
        var hora       = esMantenimiento ? HoraMantenimiento       : Hora;
        var diaSemana  = esMantenimiento ? DiaSemanaMantenimiento  : DiaSemana;

        if (frecuencia == "DIARIO")
        {
            var hoy  = ahora.Date.AddHours(hora);
            var prox = ahora < hoy ? hoy : hoy.AddDays(1);
            return prox.ToString("yyyy-MM-dd HH:mm") + " UTC";
        }
        else // SEMANAL
        {
            var diasHasta = ((diaSemana - (int)ahora.DayOfWeek) + 7) % 7;
            var prox      = ahora.Date.AddDays(diasHasta).AddHours(hora);
            if (prox <= ahora) prox = prox.AddDays(7);
            return prox.ToString("yyyy-MM-dd HH:mm") + " UTC";
        }
    }
}
