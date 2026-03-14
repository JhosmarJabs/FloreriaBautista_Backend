using System.Diagnostics;
using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Database;
using FloreriaBautista.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Services.Database;

public class DatabaseHealthService : IDatabaseHealthService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _config;

    public DatabaseHealthService(AppDbContext context, IConfiguration config)
    {
        _context = context;
        _config  = config;
    }

    public async Task<HealthResponseDto> VerificarConexionAsync()
    {
        var dto = new HealthResponseDto { ConsultadoEn = DateTime.UtcNow };
        var sw  = Stopwatch.StartNew();

        try
        {
            // ── Verificar conexión ─────────────────────────────────
            var puedeConectar = await _context.Database.CanConnectAsync();
            sw.Stop();

            if (!puedeConectar)
            {
                dto.Conectado      = false;
                dto.Estado         = "ERROR";
                dto.MensajeError   = "No se pudo establecer conexión con la base de datos.";
                dto.TiempoRespuesta = $"{sw.ElapsedMilliseconds} ms";
                return dto;
            }

            // ── Consultar datos del servidor ───────────────────────
            var info = await _context.Database
                .SqlQueryRaw<DbServerInfo>(@"
                    SELECT
                        version()                                         AS version,
                        current_database()                                AS basedatos,
                        COALESCE(inet_server_addr()::text, 'localhost')   AS servidor,
                        (SELECT count(*)::int FROM pg_stat_activity
                         WHERE state IS NOT NULL)                         AS conexionesactivas,
                        (SELECT setting::int FROM pg_settings
                         WHERE name = 'max_connections')                  AS conexionesmaximas,
                        pg_postmaster_start_time()::text                  AS inicioservidor
                ")
                .FirstOrDefaultAsync();

            dto.Conectado         = true;
            dto.Estado            = "OK";
            dto.TiempoRespuesta   = $"{sw.ElapsedMilliseconds} ms";
            dto.VersionPostgres   = info?.version   ?? "N/D";
            dto.BaseDatos         = info?.basedatos  ?? "N/D";
            dto.Servidor          = info?.servidor   ?? "localhost";
            dto.ConexionesActivas = info?.conexionesactivas ?? 0;
            dto.ConexionesMaximas = info?.conexionesmaximas ?? 0;

            // Calcular uptime
            if (info?.inicioservidor is not null &&
                DateTime.TryParse(info.inicioservidor, out var inicio))
            {
                var uptime = DateTime.UtcNow - inicio.ToUniversalTime();
                dto.TiempoActividad = $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            dto.Conectado       = false;
            dto.Estado          = "ERROR";
            dto.MensajeError    = ex.Message;
            dto.TiempoRespuesta = $"{sw.ElapsedMilliseconds} ms";
        }

        return dto;
    }

    // Clase auxiliar para mapear el resultado de la query
    private class DbServerInfo
    {
        public string version          { get; set; } = string.Empty;
        public string basedatos        { get; set; } = string.Empty;
        public string servidor         { get; set; } = string.Empty;
        public int    conexionesactivas { get; set; }
        public int    conexionesmaximas { get; set; }
        public string inicioservidor   { get; set; } = string.Empty;
    }
}
