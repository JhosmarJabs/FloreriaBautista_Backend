using System.Diagnostics;
using Npgsql;
using FloreriaBautista.Models.DTOs.Database;
using FloreriaBautista.Services.Interfaces;

namespace FloreriaBautista.Services.Database;

public class DatabaseMaintenanceService : IDatabaseMaintenanceService
{
    private readonly ILogger<DatabaseMaintenanceService> _logger;

    public DatabaseMaintenanceService(ILogger<DatabaseMaintenanceService> logger)
    {
        _logger = logger;
    }

    // ── VACUUM ANALYZE ────────────────────────────────────────────
    // VACUUM no puede correr dentro de transacción ni pipeline de EF Core
    // Usa conexión Npgsql propia con app_user (que tiene VACUUM ANALYZE)
    public async Task<MaintenanceResponseDto> EjecutarVacuumAsync()
    {
        var sw  = Stopwatch.StartNew();
        var dto = new MaintenanceResponseDto { Tarea = "VACUUM ANALYZE", EjecutadoEn = DateTime.UtcNow };

        try
        {
            await using var conn = CrearConexionAdmin();
            await conn.OpenAsync();
            await SetSearchPath(conn);

            // Obtener tablas
            var tablas = new List<string>();
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT tablename FROM pg_tables WHERE schemaname = 'public' ORDER BY tablename";
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    tablas.Add(reader.GetString(0));
            }

            foreach (var tabla in tablas)
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $"VACUUM ANALYZE public.{tabla}";
                await cmd.ExecuteNonQueryAsync();
                dto.Resultados.Add($"✔ VACUUM ANALYZE {tabla}");
            }

            sw.Stop();
            dto.Estado     = "COMPLETADO";
            dto.Detalle    = $"VACUUM ANALYZE ejecutado en {tablas.Count} tablas.";
            dto.DuracionMs = sw.Elapsed.TotalMilliseconds;
        }
        catch (Exception ex)
        {
            sw.Stop();
            dto.Estado       = "ERROR";
            dto.MensajeError = ex.Message;
            dto.DuracionMs   = sw.Elapsed.TotalMilliseconds;
            _logger.LogError(ex, "Error en VACUUM ANALYZE");
        }

        return dto;
    }

    // ── REINDEX ───────────────────────────────────────────────────
    // Requiere db_admin_user (permisos de owner sobre las tablas)
    public async Task<MaintenanceResponseDto> ReindexarAsync()
    {
        var sw  = Stopwatch.StartNew();
        var dto = new MaintenanceResponseDto { Tarea = "REINDEX", EjecutadoEn = DateTime.UtcNow };

        try
        {
            await using var conn = CrearConexionAdmin();
            await conn.OpenAsync();
            await SetSearchPath(conn);

            var tablas = new List<string>();
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT tablename FROM pg_tables WHERE schemaname = 'public' ORDER BY tablename";
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    tablas.Add(reader.GetString(0));
            }

            foreach (var tabla in tablas)
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $"REINDEX TABLE public.{tabla}";
                await cmd.ExecuteNonQueryAsync();
                dto.Resultados.Add($"✔ REINDEX TABLE {tabla}");
            }

            sw.Stop();
            dto.Estado     = "COMPLETADO";
            dto.Detalle    = $"Reindexado ejecutado en {tablas.Count} tablas.";
            dto.DuracionMs = sw.Elapsed.TotalMilliseconds;
        }
        catch (Exception ex)
        {
            sw.Stop();
            dto.Estado       = "ERROR";
            dto.MensajeError = ex.Message;
            dto.DuracionMs   = sw.Elapsed.TotalMilliseconds;
            _logger.LogError(ex, "Error en REINDEX");
        }

        return dto;
    }

    // ── LIMPIEZA DE REGISTROS ANTIGUOS ────────────────────────────
    public async Task<MaintenanceResponseDto> LimpiarRegistrosAntiguosAsync(int diasAntiguedad = 90)
    {
        var sw    = Stopwatch.StartNew();
        var dto   = new MaintenanceResponseDto { Tarea = "LIMPIEZA", EjecutadoEn = DateTime.UtcNow };
        var corte = DateTime.UtcNow.AddDays(-diasAntiguedad);
        var ahora = DateTime.UtcNow;

        try
        {
            await using var conn = CrearConexionAdmin();
            await conn.OpenAsync();
            await SetSearchPath(conn);

            async Task<int> Ejecutar(string sql, DateTime param)
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("p", param);
                return await cmd.ExecuteNonQueryAsync();
            }

            dto.Resultados.Add($"✔ audit_logs: {await Ejecutar("DELETE FROM public.audit_logs WHERE fecha_hora < @p", corte)} eliminados");
            dto.Resultados.Add($"✔ backup_jobs: {await Ejecutar("DELETE FROM public.backup_jobs WHERE estado IN ('COMPLETADO','ERROR') AND creado_en < @p", corte)} eliminados");
            dto.Resultados.Add($"✔ import_jobs: {await Ejecutar("DELETE FROM public.import_jobs WHERE estado IN ('COMPLETADO','ERROR') AND creado_en < @p", corte)} eliminados");
            dto.Resultados.Add($"✔ auth_tokens: {await Ejecutar("DELETE FROM public.auth_tokens WHERE expira_en < @p", ahora)} eliminados");

            sw.Stop();
            dto.Estado     = "COMPLETADO";
            dto.Detalle    = $"Limpieza de registros anteriores a {diasAntiguedad} días completada.";
            dto.DuracionMs = sw.Elapsed.TotalMilliseconds;
        }
        catch (Exception ex)
        {
            sw.Stop();
            dto.Estado       = "ERROR";
            dto.MensajeError = ex.Message;
            dto.DuracionMs   = sw.Elapsed.TotalMilliseconds;
            _logger.LogError(ex, "Error en limpieza");
        }

        return dto;
    }

    // ── ANALYZE ───────────────────────────────────────────────────
    public async Task<MaintenanceResponseDto> ActualizarEstadisticasAsync()
    {
        var sw  = Stopwatch.StartNew();
        var dto = new MaintenanceResponseDto { Tarea = "ANALYZE", EjecutadoEn = DateTime.UtcNow };

        try
        {
            await using var conn = CrearConexionAdmin();
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "ANALYZE";
            await cmd.ExecuteNonQueryAsync();

            sw.Stop();
            dto.Estado     = "COMPLETADO";
            dto.Detalle    = "Estadísticas del query planner actualizadas correctamente.";
            dto.DuracionMs = sw.Elapsed.TotalMilliseconds;
            dto.Resultados.Add("✔ ANALYZE ejecutado en toda la base de datos");
        }
        catch (Exception ex)
        {
            sw.Stop();
            dto.Estado       = "ERROR";
            dto.MensajeError = ex.Message;
            dto.DuracionMs   = sw.Elapsed.TotalMilliseconds;
            _logger.LogError(ex, "Error en ANALYZE");
        }

        return dto;
    }

    // ── MANTENIMIENTO COMPLETO ────────────────────────────────────
    public async Task<List<MaintenanceResponseDto>> EjecutarMantenimientoCompletoAsync()
    {
        _logger.LogInformation("Iniciando mantenimiento completo...");
        var resultados = new List<MaintenanceResponseDto>
        {
            await LimpiarRegistrosAntiguosAsync(90),
            await ActualizarEstadisticasAsync(),
            await EjecutarVacuumAsync(),
            await ReindexarAsync()
        };
        _logger.LogInformation("Mantenimiento completo finalizado.");
        return resultados;
    }

    // ── Helpers de conexión ───────────────────────────────────────
    private static NpgsqlConnection CrearConexionApp()
    {
        var cs = $"Host={Env("DB_HOST")};Port={Env("DB_PORT")};Database={Env("DB_NAME")};" +
                 $"Username={Env("DB_USER")};Password={Env("DB_PASSWORD")};Search Path=public";
        return new NpgsqlConnection(cs);
    }

    private static NpgsqlConnection CrearConexionAdmin()
    {
        var cs = $"Host={Env("DB_HOST")};Port={Env("DB_PORT")};Database={Env("DB_NAME")};" +
                 $"Username={Env("DB_ADMIN_USER")};Password={Env("DB_ADMIN_PASSWORD")};Search Path=public";
        return new NpgsqlConnection(cs);
    }

    private static async Task SetSearchPath(NpgsqlConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SET search_path = public";
        await cmd.ExecuteNonQueryAsync();
    }

    private static string Env(string key) =>
        Environment.GetEnvironmentVariable(key)
        ?? throw new InvalidOperationException($"Variable '{key}' no configurada en .env");
}
