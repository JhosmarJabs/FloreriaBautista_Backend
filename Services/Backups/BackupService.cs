using System.Diagnostics;
using Npgsql;
using FloreriaBautista.Models.DTOs.Backups;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Services.Interfaces;

namespace FloreriaBautista.Services.Backups;

/// <summary>
/// Genera backups con pg_dump en formato custom (.backup).
/// Usa /tmp como carpeta temporal — funciona en cualquier SO (Linux, Windows, Docker).
/// El archivo se sube a Google Drive y se elimina del disco inmediatamente.
/// </summary>
public class BackupService : IBackupService
{
    private readonly GoogleDriveService     _driveService;
    private readonly ILogger<BackupService> _logger;

    private static readonly HashSet<string> TablasExcluidas =
        new(StringComparer.OrdinalIgnoreCase) { "schema_migrations", "__efmigrationshistory" };

    public BackupService(GoogleDriveService driveService, ILogger<BackupService> logger)
    {
        _driveService = driveService;
        _logger       = logger;
    }

    // ── Tablas disponibles ────────────────────────────────────────
    public async Task<List<string>> ObtenerTablasAsync()
    {
        var resultado = new List<string>();
        await using var conn = CrearConexion();
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT table_name FROM information_schema.tables " +
            "WHERE table_schema = 'public' AND table_type = 'BASE TABLE' " +
            "ORDER BY table_name";

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            resultado.Add(reader.GetString(0));

        return resultado.Where(t => !TablasExcluidas.Contains(t)).ToList();
    }

    // ── Backup FULL ───────────────────────────────────────────────
    public async Task<BackupResponseDto> CrearBackupFullAsync(string? descripcion, Guid usuarioId)
    {
        var inicio  = DateTime.UtcNow;
        var nombre  = GenerarNombreArchivo("full", null);
        var tmpPath = Path.Combine(Path.GetTempPath(), nombre);

        try
        {
            await EjecutarPgDumpAsync(tmpPath, tabla: null);
            _logger.LogInformation("Backup FULL generado en tmp: {Path}", tmpPath);

            var (driveId, driveEnlace) = await SubirADriveAsync(tmpPath, nombre);
            var tamano = new FileInfo(tmpPath).Length;

            return new BackupResponseDto
            {
                Id           = Guid.NewGuid(),
                Tipo         = "BD",
                Estado       = "COMPLETADO",
                CreadoEn     = inicio,
                CompletadoEn = DateTime.UtcNow,
                TamanoBytes  = tamano,
                DriveFileId  = driveId,
                DriveEnlace  = driveEnlace,
                SubidoADrive = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear backup FULL");
            return new BackupResponseDto
            {
                Id           = Guid.NewGuid(),
                Tipo         = "BD",
                Estado       = "ERROR",
                CreadoEn     = inicio,
                CompletadoEn = DateTime.UtcNow,
                MensajeError = ex.Message
            };
        }
        finally
        {
            // Eliminar archivo temporal siempre
            if (File.Exists(tmpPath))
            {
                File.Delete(tmpPath);
                _logger.LogInformation("Archivo temporal eliminado: {Path}", tmpPath);
            }
        }
    }

    // ── Backup por tabla ──────────────────────────────────────────
    public async Task<BackupResponseDto> CrearBackupTablaAsync(
        string nombreTabla, string? descripcion, Guid usuarioId)
    {
        var tablas = await ObtenerTablasAsync();
        if (!tablas.Contains(nombreTabla, StringComparer.OrdinalIgnoreCase))
            throw new AppException($"La tabla '{nombreTabla}' no existe en la base de datos.");

        var inicio  = DateTime.UtcNow;
        var nombre  = GenerarNombreArchivo("tabla", nombreTabla);
        var tmpPath = Path.Combine(Path.GetTempPath(), nombre);

        try
        {
            await EjecutarPgDumpAsync(tmpPath, tabla: nombreTabla);
            _logger.LogInformation("Backup tabla '{Tabla}' generado en tmp: {Path}", nombreTabla, tmpPath);

            var (driveId, driveEnlace) = await SubirADriveAsync(tmpPath, nombre);
            var tamano = new FileInfo(tmpPath).Length;

            return new BackupResponseDto
            {
                Id           = Guid.NewGuid(),
                Tipo         = "BD_ARCHIVOS",
                Estado       = "COMPLETADO",
                NombreTabla  = nombreTabla,
                CreadoEn     = inicio,
                CompletadoEn = DateTime.UtcNow,
                TamanoBytes  = tamano,
                DriveFileId  = driveId,
                DriveEnlace  = driveEnlace,
                SubidoADrive = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear backup de tabla '{Tabla}'", nombreTabla);
            return new BackupResponseDto
            {
                Id           = Guid.NewGuid(),
                Tipo         = "BD_ARCHIVOS",
                Estado       = "ERROR",
                NombreTabla  = nombreTabla,
                CreadoEn     = inicio,
                CompletadoEn = DateTime.UtcNow,
                MensajeError = ex.Message
            };
        }
        finally
        {
            if (File.Exists(tmpPath))
            {
                File.Delete(tmpPath);
                _logger.LogInformation("Archivo temporal eliminado: {Path}", tmpPath);
            }
        }
    }

    // ── Listar backups en Drive ───────────────────────────────────
    public async Task<List<BackupResponseDto>> ListarBackupsAsync()
    {
        var archivos = await _driveService.ListarArchivosAsync();
        return archivos.Select(f => new BackupResponseDto
        {
            Id           = Guid.NewGuid(),
            Tipo         = f.Nombre?.Contains("_full_") == true ? "BD" : "BD_ARCHIVOS",
            Estado       = "COMPLETADO",
            CreadoEn     = f.CreadoEn ?? DateTime.UtcNow,
            CompletadoEn = f.CreadoEn ?? DateTime.UtcNow,
            TamanoBytes  = f.TamanoBytes,
            DriveFileId  = f.Id,
            DriveEnlace  = f.Enlace,
            SubidoADrive = true
        }).ToList();
    }

    public async Task<List<DriveFileDto>> ListarArchivosDriveAsync()
        => await _driveService.ListarArchivosAsync();

    public Task<BackupResponseDto> ObtenerBackupAsync(Guid id)
        => throw new NotFoundException("BackupJob", id);

    // ── pg_dump ───────────────────────────────────────────────────
    private async Task EjecutarPgDumpAsync(string rutaArchivo, string? tabla)
    {
        var host     = Env("DB_HOST");
        var port     = Env("DB_PORT");
        var database = Env("DB_NAME");
        var user     = Environment.GetEnvironmentVariable("BACKUP_DB_USER") ?? Env("DB_USER");
        var password = Environment.GetEnvironmentVariable("BACKUP_DB_PASSWORD") ?? Env("DB_PASSWORD");

        var args = tabla is null
            ? $"-h {host} -p {port} -U {user} -F c -f \"{rutaArchivo}\" {database}"
            : $"-h {host} -p {port} -U {user} -F c --table=public.{tabla} -f \"{rutaArchivo}\" {database}";

        var psi = new ProcessStartInfo
        {
            FileName              = "pg_dump",
            Arguments             = args,
            RedirectStandardError = true,
            UseShellExecute       = false,
            CreateNoWindow        = true
        };
        psi.Environment["PGPASSWORD"] = password;

        using var process = Process.Start(psi)
            ?? throw new AppException("No se pudo iniciar pg_dump.");

        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new AppException($"pg_dump falló (código {process.ExitCode}): {stderr}");
    }

    // ── Subir a Drive ─────────────────────────────────────────────
    private async Task<(string DriveId, string DriveEnlace)> SubirADriveAsync(
        string rutaArchivo, string nombre)
    {
        var driveId = await _driveService.SubirArchivoAsync(rutaArchivo, nombre);
        var enlace  = $"https://drive.google.com/file/d/{driveId}/view";
        _logger.LogInformation("Backup subido a Drive. ID: {Id}", driveId);
        return (driveId, enlace);
    }

    // ── Helpers ───────────────────────────────────────────────────
    private static NpgsqlConnection CrearConexion()
    {
        var host     = Env("DB_HOST");
        var port     = Env("DB_PORT");
        var database = Env("DB_NAME");
        var user     = Environment.GetEnvironmentVariable("BACKUP_DB_USER") ?? Env("DB_USER");
        var password = Environment.GetEnvironmentVariable("BACKUP_DB_PASSWORD") ?? Env("DB_PASSWORD");
        return new NpgsqlConnection(
            $"Host={host};Port={port};Database={database};Username={user};Password={password};Search Path=public");
    }

    private static string GenerarNombreArchivo(string tipo, string? tabla)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
        var database  = Environment.GetEnvironmentVariable("DB_NAME") ?? "floreria_bautista";
        return tipo == "full"
            ? $"backup_{database}_full_{timestamp}.backup"
            : $"backup_{database}_{tabla}_{timestamp}.backup";
    }

    private static string Env(string key) =>
        Environment.GetEnvironmentVariable(key)
        ?? throw new InvalidOperationException($"Variable '{key}' no configurada en .env");
}
