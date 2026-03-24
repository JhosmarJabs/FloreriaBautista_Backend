namespace FloreriaBautista.Services.Scheduler;

/// <summary>
/// Configuración en memoria del scheduler — se inicializa desde .env
/// y puede modificarse en tiempo real desde el endpoint de admin.
/// Se pierde al reiniciar (las variables de entorno siguen siendo el default).
/// </summary>
public class SchedulerConfig
{
    public bool   BackupAutomaticoActivo { get; set; }
    public string Frecuencia             { get; set; } = "SEMANAL";
    public int    DiaSemana              { get; set; }
    public int    Hora                   { get; set; }
    public bool   MantenimientoActivo    { get; set; } = true;

    // Singleton — inicializa desde .env
    public static SchedulerConfig CargarDesdeEnv() => new()
    {
        BackupAutomaticoActivo = true,
        Frecuencia             = Environment.GetEnvironmentVariable("BACKUP_FRECUENCIA") ?? "SEMANAL",
        DiaSemana              = int.TryParse(Environment.GetEnvironmentVariable("BACKUP_DIA_SEMANA"), out var d) ? d : 0,
        Hora                   = int.TryParse(Environment.GetEnvironmentVariable("BACKUP_HORA"), out var h) ? h : 0,
        MantenimientoActivo    = true
    };

    private static readonly string[] NombresDias =
        ["Domingo", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado"];

    public string NombreDia   => DiaSemana >= 0 && DiaSemana < 7 ? NombresDias[DiaSemana] : "?";
    public string HoraFormato => $"{Hora:D2}:00";

    public string ProximaEjecucion(bool esMantenimiento = false)
    {
        if (!BackupAutomaticoActivo) return "Desactivado";

        var ahora  = DateTime.UtcNow;
        var hora   = esMantenimiento ? Hora + 1 : Hora;

        if (Frecuencia == "DIARIO")
        {
            var hoy = ahora.Date.AddHours(hora);
            var prox = ahora < hoy ? hoy : hoy.AddDays(1);
            return prox.ToString("yyyy-MM-dd HH:mm") + " UTC";
        }
        else // SEMANAL
        {
            var diasHasta = ((DiaSemana - (int)ahora.DayOfWeek) + 7) % 7;
            var prox      = ahora.Date.AddDays(diasHasta).AddHours(hora);
            if (prox <= ahora) prox = prox.AddDays(7);
            return prox.ToString("yyyy-MM-dd HH:mm") + " UTC";
        }
    }
}
