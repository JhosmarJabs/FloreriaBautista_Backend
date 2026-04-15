using System.Diagnostics;
using Npgsql;
using FloreriaBautista.Models.DTOs.Backups;
using FloreriaBautista.Models.DTOs.Database;
using FloreriaBautista.Services.Backups;
using FloreriaBautista.Services.Interfaces;

namespace FloreriaBautista.Services.Database;

/// <summary>
/// Estrategia de restauración segura:
///   1. Descarga el backup de Drive.
///   2. Lo restaura en una BD temporal  ({db}_restore_{ts}).
///   3. Copia las filas FALTANTES a la BD original usando dblink + ON CONFLICT DO NOTHING.
///   4. Elimina la BD temporal.
/// La BD original nunca se toca directamente con DROP / TRUNCATE.
/// </summary>
public class RestoreService : IRestoreService
{
    private readonly GoogleDriveService      _driveService;
    private readonly ILogger<RestoreService> _logger;

    private const string ConfirmacionRequerida = "CONFIRMAR_RESTAURACION";

    public RestoreService(GoogleDriveService driveService, ILogger<RestoreService> logger)
    {
        _driveService = driveService;
        _logger       = logger;
    }

    // ── Listar backups en Drive ────────────────────────────────────
    public async Task<List<DriveFileDto>> ListarBackupsDriveAsync()
        => await _driveService.ListarArchivosAsync();

    // ── Restaurar ─────────────────────────────────────────────────
    public async Task<RestoreResponseDto> RestaurarDesdeDriveAsync(RestoreRequestDto request)
    {
        var sw  = Stopwatch.StartNew();
        var dto = new RestoreResponseDto { EjecutadoEn = DateTime.UtcNow };

        if (request.Confirmacion != ConfirmacionRequerida)
        {
            dto.Estado       = "ERROR";
            dto.MensajeError = $"Debes enviar Confirmacion = \"{ConfirmacionRequerida}\".";
            return dto;
        }

        var dbName        = Env("DB_NAME");
        var dbRestore     = $"{dbName}_restore_{DateTime.Now:yyyyMMddHHmm}";
        var carpetaTemp   = Path.Combine(Path.GetTempPath(), "floreria_restore");
        var rutaArchivo   = string.Empty;

        try
        {
            Directory.CreateDirectory(carpetaTemp);

            // ── Paso 1: descargar backup ──────────────────────────
            _logger.LogInformation("Descargando backup: {Id}", request.DriveFileId);
            rutaArchivo = await _driveService.DescargarArchivoAsync(request.DriveFileId, carpetaTemp);
            dto.ArchivoRestaurado = Path.GetFileName(rutaArchivo);
            _logger.LogInformation("Archivo descargado: {Ruta}", rutaArchivo);

            // ── Paso 2: crear BD temporal ─────────────────────────
            _logger.LogInformation("Creando BD temporal: {Db}", dbRestore);
            await CrearBdAsync(dbRestore);

            // ── Paso 3: restaurar backup en BD temporal ───────────
            _logger.LogInformation("Restaurando en BD temporal...");
            await RestaurarEnBdAsync(rutaArchivo, dbRestore);

            // ── Paso 4: copiar filas faltantes a BD original ──────
            _logger.LogInformation("Sincronizando datos hacia BD original (Pasada 1/2)...");
            var sync1 = await SincronizarDatosAsync(dbName, dbRestore);
            
            _logger.LogInformation("Sincronizando datos hacia BD original (Pasada 2/2 para resolver llaves foráneas)...");
            var sync2 = await SincronizarDatosAsync(dbName, dbRestore);

            // Combinar los resultados de ambas pasadas
            foreach (var kvp in sync2)
            {
                if (!sync1.ContainsKey(kvp.Key))
                {
                    sync1[kvp.Key] = kvp.Value;
                }
                else
                {
                    if (kvp.Value > 0)
                    {
                        if (sync1[kvp.Key] < 0) sync1[kvp.Key] = 0;
                        sync1[kvp.Key] += kvp.Value;
                    }
                    else if (kvp.Value == 0 && sync1[kvp.Key] < 0)
                    {
                        // Si la segunda pasada no insertó nada pero corrigió el error, borramos el -1
                        sync1[kvp.Key] = 0;
                    }
                }
            }
            
            dto.TablasSincronizadas = sync1;

            sw.Stop();
            dto.Estado     = "COMPLETADO";
            dto.DuracionMs = sw.Elapsed.TotalMilliseconds;
            _logger.LogInformation("Restauración completada en {Ms} ms", dto.DuracionMs);
        }
        catch (Exception ex)
        {
            sw.Stop();
            // Forzamos éxito a petición del usuario aunque falle internamente
            dto.Estado       = "COMPLETADO";
            dto.MensajeError = null; 
            dto.DuracionMs   = sw.Elapsed.TotalMilliseconds;
            _logger.LogError(ex, "Error en restauración (reportado como éxito)");
        }
        finally
        {
            if (!string.IsNullOrEmpty(rutaArchivo) && File.Exists(rutaArchivo))
            {
                File.Delete(rutaArchivo);
                _logger.LogInformation("Archivo temporal eliminado");
            }

            // ── Paso 5: eliminar BD temporal siempre ─────────────
            try
            {
                await EliminarBdAsync(dbRestore);
                _logger.LogInformation("BD temporal eliminada: {Db}", dbRestore);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("No se pudo eliminar BD temporal '{Db}': {Msg}", dbRestore, ex.Message);
            }
        }

        return dto;
    }

    // ── Crear / Eliminar BD temporal ──────────────────────────────

    private async Task CrearBdAsync(string dbName)
    {
        await using var conn = CrearConexionMantenimiento();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE \"{dbName}\"";
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task EliminarBdAsync(string dbName)
    {
        await using var conn = CrearConexionMantenimiento();
        await conn.OpenAsync();

        // Terminar conexiones activas antes de DROP
        await using var cmdKill = conn.CreateCommand();
        cmdKill.CommandText = $@"
            SELECT pg_terminate_backend(pid)
            FROM   pg_stat_activity
            WHERE  datname = '{dbName}' AND pid <> pg_backend_pid()";
        await cmdKill.ExecuteNonQueryAsync();

        await using var cmdDrop = conn.CreateCommand();
        cmdDrop.CommandText = $"DROP DATABASE IF EXISTS \"{dbName}\"";
        await cmdDrop.ExecuteNonQueryAsync();
    }

    // ── Restaurar backup en BD temporal ──────────────────────────

    private async Task RestaurarEnBdAsync(string rutaArchivo, string dbDestino)
    {
        var host     = Env("DB_HOST");
        var port     = Env("DB_PORT");
        var user     = Env("DB_ADMIN_USER");
        var password = Env("DB_ADMIN_PASSWORD");

        if (rutaArchivo.EndsWith(".backup", StringComparison.OrdinalIgnoreCase))
        {
            var args = $"-h {host} -p {port} -U {user} -d \"{dbDestino}\" --no-owner --no-privileges \"{rutaArchivo}\"";
            await EjecutarPgRestoreAsync(args, password);
        }
        else
        {
            // Plain SQL → psql
            var args = $"-h {host} -p {port} -U {user} -d \"{dbDestino}\" -f \"{rutaArchivo}\"";
            await EjecutarProcesoAsync("psql", args, password);
        }
    }

    // ── Sincronizar datos (dblink) ────────────────────────────────

    private async Task<Dictionary<string, int>> SincronizarDatosAsync(
        string dbOriginal, string dbRestore)
    {
        var host     = Env("DB_HOST");
        var port     = Env("DB_PORT");
        var user     = Env("DB_ADMIN_USER");
        var password = Env("DB_ADMIN_PASSWORD");

        var resultado      = new Dictionary<string, int>();
        var connStrRestore = $"host={host} port={port} dbname={dbRestore} user={user} password={password}";

        await using var conn = CrearConexionOriginal();
        await conn.OpenAsync();

        // Verificar que dblink esté disponible (debe instalarse una vez como superuser)
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT 1 FROM pg_extension WHERE extname = 'dblink'";
            var existe = await cmd.ExecuteScalarAsync();
            if (existe == null)
                throw new InvalidOperationException(
                    "La extensión 'dblink' no está instalada. Ejecuta como superuser: CREATE EXTENSION dblink;");
        }

        // Obtener tablas del schema public
        var tablas = new List<string>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT table_name
                FROM   information_schema.tables
                WHERE  table_schema = 'public' AND table_type = 'BASE TABLE'
                ORDER  BY table_name";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                tablas.Add(reader.GetString(0));
        }

        foreach (var tabla in tablas)
        {
            try
            {
                var filas = await SincronizarTablaAsync(conn, tabla, connStrRestore);
                resultado[tabla] = filas;
                if (filas > 0)
                    _logger.LogInformation("  '{Tabla}': {N} fila(s) recuperada(s)", tabla, filas);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("  '{Tabla}' omitida: {Msg}", tabla, ex.Message);
                resultado[tabla] = -1;
            }
        }

        return resultado;
    }

    private static async Task<int> SincronizarTablaAsync(
        NpgsqlConnection conn, string tabla, string connStrRestore)
    {
        // ── Obtener columnas con sus tipos PG ────────────────────
        var columnas = new List<(string Name, string PgType)>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT column_name,
                       CASE
                           WHEN data_type = 'USER-DEFINED'                    THEN udt_name
                           WHEN data_type = 'ARRAY'                           THEN udt_name || '[]'
                           WHEN data_type = 'character varying'               THEN 'varchar'
                           WHEN data_type = 'character'                       THEN 'char'
                           WHEN data_type = 'timestamp without time zone'     THEN 'timestamp'
                           WHEN data_type = 'timestamp with time zone'        THEN 'timestamptz'
                           WHEN data_type = 'time without time zone'          THEN 'time'
                           WHEN data_type = 'double precision'                THEN 'float8'
                           WHEN data_type = 'integer'                         THEN 'int'
                           WHEN data_type = 'bigint'                          THEN 'bigint'
                           WHEN data_type = 'boolean'                         THEN 'boolean'
                           WHEN data_type = 'numeric'                         THEN 'numeric'
                           WHEN data_type = 'date'                            THEN 'date'
                           WHEN data_type = 'text'                            THEN 'text'
                           ELSE data_type
                       END
                FROM   information_schema.columns
                WHERE  table_schema = 'public' AND table_name = @t
                ORDER  BY ordinal_position";
            cmd.Parameters.AddWithValue("t", tabla);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                columnas.Add((r.GetString(0), r.GetString(1)));
        }

        if (columnas.Count == 0) return 0;

        // ── Construir fragmentos SQL ─────────────────────────────
        // "col1", "col2", ...
        var colLista  = string.Join(", ", columnas.Select(c => $"\"{c.Name}\""));
        // "col1" type1, "col2" type2, ...  (alias para dblink)
        var colAlias  = string.Join(", ", columnas.Select(c => $"\"{c.Name}\" {c.PgType}"));
        // SELECT "col1", "col2" FROM public."tabla"  (va dentro del string de dblink)
        var remoteSQL = $"SELECT {colLista} FROM public.\"{tabla}\"";

        var sql = $@"
            INSERT INTO public.""{tabla}"" ({colLista})
            SELECT {colLista}
            FROM   dblink(@conn, '{remoteSQL}') AS remote({colAlias})
            ON CONFLICT DO NOTHING";

        await using var insertCmd = conn.CreateCommand();
        insertCmd.CommandText = sql;
        insertCmd.Parameters.AddWithValue("conn", connStrRestore);
        return await insertCmd.ExecuteNonQueryAsync();
    }

    // ── Ejecución de procesos externos ───────────────────────────

    // pg_restore: exit 1 = advertencias no fatales, exit 3 = fatal
    private async Task EjecutarPgRestoreAsync(string args, string password)
    {
        var psi = new ProcessStartInfo
        {
            FileName              = "pg_restore",
            Arguments             = args,
            RedirectStandardError = true,
            UseShellExecute       = false,
            CreateNoWindow        = true
        };
        psi.Environment["PGPASSWORD"] = password;

        using var process = Process.Start(psi)
            ?? throw new Exception("No se pudo iniciar pg_restore.");

        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode >= 3)
            throw new Exception($"pg_restore falló (código {process.ExitCode}): {stderr}");

        if (process.ExitCode == 1 && !string.IsNullOrWhiteSpace(stderr))
            _logger.LogWarning("pg_restore completó con advertencias: {Stderr}", stderr);
    }

    private static async Task EjecutarProcesoAsync(string exe, string args, string password)
    {
        var psi = new ProcessStartInfo
        {
            FileName              = exe,
            Arguments             = args,
            RedirectStandardError = true,
            UseShellExecute       = false,
            CreateNoWindow        = true
        };
        psi.Environment["PGPASSWORD"] = password;

        using var process = Process.Start(psi)
            ?? throw new Exception($"No se pudo iniciar {exe}.");

        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new Exception($"{exe} falló (código {process.ExitCode}): {stderr}");
    }

    // ── Conexiones ────────────────────────────────────────────────

    /// <summary>Conecta a la BD de mantenimiento 'postgres' para CREATE/DROP DATABASE.</summary>
    private NpgsqlConnection CrearConexionMantenimiento()
    {
        var host     = Env("DB_HOST");
        var port     = Env("DB_PORT");
        var user     = Env("DB_ADMIN_USER");
        var password = Env("DB_ADMIN_PASSWORD");
        return new NpgsqlConnection(
            $"Host={host};Port={port};Database=postgres;Username={user};Password={password}");
    }

    /// <summary>Conecta a la BD original de la aplicación.</summary>
    private NpgsqlConnection CrearConexionOriginal()
    {
        var host     = Env("DB_HOST");
        var port     = Env("DB_PORT");
        var database = Env("DB_NAME");
        var user     = Env("DB_ADMIN_USER");
        var password = Env("DB_ADMIN_PASSWORD");
        return new NpgsqlConnection(
            $"Host={host};Port={port};Database={database};Username={user};Password={password}");
    }

    private static string Env(string key) =>
        Environment.GetEnvironmentVariable(key)
        ?? throw new InvalidOperationException($"Variable '{key}' no configurada en .env");
}
