using System.Diagnostics;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Backups;
using FloreriaBautista.Models.Entities;
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
    private readonly AppDbContext           _context;
    private readonly GoogleDriveService     _driveService;
    private readonly ILogger<BackupService> _logger;

    private static readonly HashSet<string> TablasExcluidas =
        new(StringComparer.OrdinalIgnoreCase) { "schema_migrations", "__efmigrationshistory" };

    public BackupService(AppDbContext context, GoogleDriveService driveService, ILogger<BackupService> logger)
    {
        _context      = context;
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
    public async Task<BackupResponseDto> CrearBackupFullAsync(string? descripcion, Guid usuarioId, string formato = "BACKUP")
    {
        formato = NormalizarFormato(formato);

        var job = new BackupJob
        {
            Id          = Guid.NewGuid(),
            Tipo        = "BD",
            Estado      = "EN_PROCESO",
            UsuarioId   = await UsuarioExisteAsync(usuarioId) ? usuarioId : null,
            Descripcion = descripcion,
            Formato     = formato,
            CreadoEn    = DateTime.UtcNow
        };
        _context.BackupJobs.Add(job);
        await _context.SaveChangesAsync();

        var nombre  = GenerarNombreArchivo("full", null, formato);
        var tmpPath = Path.Combine(Path.GetTempPath(), nombre);

        try
        {
            await EjecutarPgDumpAsync(tmpPath, tabla: null, formato);
            _logger.LogInformation("Backup FULL generado en tmp: {Path}", tmpPath);

            var (driveId, driveEnlace) = await SubirADriveAsync(tmpPath, nombre);

            job.Estado       = "COMPLETADO";
            job.CompletadoEn = DateTime.UtcNow;
            job.DriveFileId  = driveId;
            job.TamanoBytes  = new FileInfo(tmpPath).Length;
            await _context.SaveChangesAsync();

            return MapToDto(job, driveEnlace);
        }
        catch (Exception ex)
        {
            job.Estado       = "ERROR";
            job.CompletadoEn = DateTime.UtcNow;
            job.MensajeError = ex.Message;
            await _context.SaveChangesAsync();

            _logger.LogError(ex, "Error al crear backup FULL");
            return MapToDto(job, null);
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

    // ── Backup por tabla ──────────────────────────────────────────
    public async Task<BackupResponseDto> CrearBackupTablaAsync(
        string nombreTabla, string? descripcion, Guid usuarioId, string formato = "BACKUP")
    {
        formato = NormalizarFormato(formato);

        var tablas = await ObtenerTablasAsync();
        if (!tablas.Contains(nombreTabla, StringComparer.OrdinalIgnoreCase))
            throw new AppException($"La tabla '{nombreTabla}' no existe en la base de datos.");

        var job = new BackupJob
        {
            Id          = Guid.NewGuid(),
            Tipo        = "BD_ARCHIVOS",
            Estado      = "EN_PROCESO",
            UsuarioId   = await UsuarioExisteAsync(usuarioId) ? usuarioId : null,
            NombreTabla = nombreTabla,
            Descripcion = descripcion,
            Formato     = formato,
            CreadoEn    = DateTime.UtcNow
        };
        _context.BackupJobs.Add(job);
        await _context.SaveChangesAsync();

        var nombre  = GenerarNombreArchivo("tabla", nombreTabla, formato);
        var tmpPath = Path.Combine(Path.GetTempPath(), nombre);

        try
        {
            await EjecutarPgDumpAsync(tmpPath, tabla: nombreTabla, formato);
            _logger.LogInformation("Backup tabla '{Tabla}' generado en tmp: {Path}", nombreTabla, tmpPath);

            var (driveId, driveEnlace) = await SubirADriveAsync(tmpPath, nombre);

            job.Estado       = "COMPLETADO";
            job.CompletadoEn = DateTime.UtcNow;
            job.DriveFileId  = driveId;
            job.TamanoBytes  = new FileInfo(tmpPath).Length;
            await _context.SaveChangesAsync();

            return MapToDto(job, driveEnlace);
        }
        catch (Exception ex)
        {
            job.Estado       = "ERROR";
            job.CompletadoEn = DateTime.UtcNow;
            job.MensajeError = ex.Message;
            await _context.SaveChangesAsync();

            _logger.LogError(ex, "Error al crear backup de tabla '{Tabla}'", nombreTabla);
            return MapToDto(job, null);
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

    // ── Listar backups desde BD ───────────────────────────────────
    public async Task<List<BackupResponseDto>> ListarBackupsAsync()
    {
        var jobs = await _context.BackupJobs
            .OrderByDescending(j => j.CreadoEn)
            .ToListAsync();

        return jobs.Select(j => MapToDto(j,
            j.DriveFileId != null
                ? $"https://drive.google.com/file/d/{j.DriveFileId}/view"
                : null)).ToList();
    }

    public async Task<List<DriveFileDto>> ListarArchivosDriveAsync()
        => await _driveService.ListarArchivosAsync();

    public async Task<BackupResponseDto> ObtenerBackupAsync(Guid id)
    {
        var job = await _context.BackupJobs.FindAsync(id)
            ?? throw new NotFoundException("BackupJob", id);

        return MapToDto(job,
            job.DriveFileId != null
                ? $"https://drive.google.com/file/d/{job.DriveFileId}/view"
                : null);
    }

    // ── Mapeo ─────────────────────────────────────────────────────
    private static BackupResponseDto MapToDto(BackupJob job, string? driveEnlace) => new()
    {
        Id           = job.Id,
        Tipo         = job.Tipo,
        Formato      = job.Formato,
        NombreTabla  = job.NombreTabla,
        Estado       = job.Estado,
        Descripcion  = job.Descripcion,
        MensajeError = job.MensajeError,
        CreadoEn     = job.CreadoEn,
        CompletadoEn = job.CompletadoEn,
        TamanoBytes  = job.TamanoBytes,
        DriveFileId  = job.DriveFileId,
        DriveEnlace  = driveEnlace,
        SubidoADrive = job.DriveFileId != null
    };

    // ── pg_dump ───────────────────────────────────────────────────
    private async Task EjecutarPgDumpAsync(string rutaArchivo, string? tabla, string formato)
    {
        var host     = Env("DB_HOST");
        var port     = Env("DB_PORT");
        var database = Env("DB_NAME");
        var user     = Environment.GetEnvironmentVariable("BACKUP_DB_USER") ?? Env("DB_USER");
        var password = Environment.GetEnvironmentVariable("BACKUP_DB_PASSWORD") ?? Env("DB_PASSWORD");

        // -F c = custom (restaurable con pg_restore) | -F p = plain SQL
        var flag = formato == "SQL" ? "-F p" : "-F c";

        var args = tabla is null
            ? $"-h {host} -p {port} -U {user} {flag} -f \"{rutaArchivo}\" {database}"
            : $"-h {host} -p {port} -U {user} {flag} --table=public.{tabla} -f \"{rutaArchivo}\" {database}";

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

    private static string GenerarNombreArchivo(string tipo, string? tabla, string formato)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
        var database  = Environment.GetEnvironmentVariable("DB_NAME") ?? "floreria_bautista";
        var extension = formato == "SQL" ? "sql" : "backup";
        return tipo == "full"
            ? $"backup_{database}_full_{timestamp}.{extension}"
            : $"backup_{database}_{tabla}_{timestamp}.{extension}";
    }

    private static string NormalizarFormato(string formato)
    {
        var upper = formato.ToUpperInvariant().Trim();
        return upper == "SQL" ? "SQL" : "BACKUP";
    }

    private Task<bool> UsuarioExisteAsync(Guid usuarioId)
        => _context.Users.AnyAsync(u => u.Id == usuarioId);

    private static string Env(string key) =>
        Environment.GetEnvironmentVariable(key)
        ?? throw new InvalidOperationException($"Variable '{key}' no configurada en .env");
}
