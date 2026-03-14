using Npgsql;
using System.Diagnostics;
using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Backups;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Services.Interfaces;

namespace FloreriaBautista.Services.Backups;

public class BackupService : IBackupService
{
    private readonly AppDbContext           _context;
    private readonly IConfiguration         _config;
    private readonly GoogleDriveService     _driveService;
    private readonly ILogger<BackupService> _logger;

    private static readonly HashSet<string> TablasExcluidas =
        new(StringComparer.OrdinalIgnoreCase) { "schema_migrations", "__efmigrationshistory" };

    public BackupService(
        AppDbContext context,
        IConfiguration config,
        GoogleDriveService driveService,
        ILogger<BackupService> logger)
    {
        _context      = context;
        _config       = config;
        _driveService = driveService;
        _logger       = logger;
    }

    // ── Tablas disponibles ────────────────────────────────────────
    public async Task<List<string>> ObtenerTablasAsync()
    {
        // Usar backup_user directamente — evita depender de app_user
        var connStr = BuildBackupConnectionString();
        var resultado = new List<string>();

        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT table_name FROM information_schema.tables " +
            "WHERE table_schema = 'public' AND table_type = 'BASE TABLE' " +
            "ORDER BY table_name";

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            resultado.Add(reader.GetString(0));

        _logger.LogInformation("Tablas encontradas: {Count}", resultado.Count);
        return resultado.Where(t => !TablasExcluidas.Contains(t)).ToList();
    }

    // ── Backup FULL ───────────────────────────────────────────────
    public async Task<BackupResponseDto> CrearBackupFullAsync(string? descripcion, Guid usuarioId)
    {
        var inicio = DateTime.UtcNow;
        var rutaArchivo = GenerarRutaArchivo("full", null);

        try
        {
            await EjecutarPgDumpAsync(rutaArchivo, tabla: null);
            _logger.LogInformation("Backup FULL generado: {Ruta}", rutaArchivo);

            var (driveId, driveEnlace) = await SubirADriveAsync(rutaArchivo, Path.GetFileName(rutaArchivo));
            var info = new FileInfo(rutaArchivo);

            return new BackupResponseDto
            {
                Id               = Guid.NewGuid(),
                Tipo             = "BD",
                Estado           = "COMPLETADO",
                CreadoEn         = inicio,
                CompletadoEn     = DateTime.UtcNow,
                RutaArchivoLocal = rutaArchivo,
                TamanoBytes      = info.Exists ? info.Length : null,
                DriveFileId      = string.IsNullOrEmpty(driveId) ? null : driveId,
                DriveEnlace      = string.IsNullOrEmpty(driveEnlace) ? null : driveEnlace,
                SubidoADrive     = !string.IsNullOrEmpty(driveId),
                MensajeError     = !string.IsNullOrEmpty(driveId) ? null : $"Drive: {driveEnlace}"
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
    }

    // ── Backup por tabla ──────────────────────────────────────────
    public async Task<BackupResponseDto> CrearBackupTablaAsync(
        string nombreTabla, string? descripcion, Guid usuarioId)
    {
        var tablas = await ObtenerTablasAsync();
        if (!tablas.Contains(nombreTabla, StringComparer.OrdinalIgnoreCase))
            throw new AppException($"La tabla '{nombreTabla}' no existe en la base de datos.");

        var inicio = DateTime.UtcNow;
        var rutaArchivo = GenerarRutaArchivo("tabla", nombreTabla);

        try
        {
            await EjecutarPgDumpAsync(rutaArchivo, tabla: nombreTabla);
            _logger.LogInformation("Backup tabla '{Tabla}' generado: {Ruta}", nombreTabla, rutaArchivo);

            var (driveId, driveEnlace) = await SubirADriveAsync(rutaArchivo, Path.GetFileName(rutaArchivo));
            var info = new FileInfo(rutaArchivo);

            return new BackupResponseDto
            {
                Id               = Guid.NewGuid(),
                Tipo             = "BD_ARCHIVOS",
                Estado           = "COMPLETADO",
                NombreTabla      = nombreTabla,
                CreadoEn         = inicio,
                CompletadoEn     = DateTime.UtcNow,
                RutaArchivoLocal = rutaArchivo,
                TamanoBytes      = info.Exists ? info.Length : null,
                DriveFileId      = string.IsNullOrEmpty(driveId) ? null : driveId,
                DriveEnlace      = string.IsNullOrEmpty(driveEnlace) ? null : driveEnlace,
                SubidoADrive     = !string.IsNullOrEmpty(driveId),
                MensajeError     = !string.IsNullOrEmpty(driveId) ? null : $"Drive: {driveEnlace}"
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
    }

    // ── Listar backups locales ────────────────────────────────────
    public async Task<List<BackupResponseDto>> ListarBackupsAsync()
    {
        var carpeta = Environment.GetEnvironmentVariable("BACKUPS_RUTA_LOCAL") ?? "backups";
        if (!Directory.Exists(carpeta))
            return [];

        return await Task.FromResult(
            Directory.GetFiles(carpeta, "*.backup")
                .Select(f =>
                {
                    var info = new FileInfo(f);
                    return new BackupResponseDto
                    {
                        Id               = Guid.NewGuid(),
                        Tipo             = info.Name.Contains("_full_") ? "BD" : "BD_ARCHIVOS",
                        Estado           = "COMPLETADO",
                        CreadoEn         = info.CreationTimeUtc,
                        CompletadoEn     = info.CreationTimeUtc,
                        RutaArchivoLocal = f,
                        TamanoBytes      = info.Length
                    };
                })
                .OrderByDescending(b => b.CreadoEn)
                .ToList()
        );
    }

    // ── Listar archivos en Drive ──────────────────────────────────
    public async Task<List<DriveFileDto>> ListarArchivosDriveAsync()
        => await _driveService.ListarArchivosAsync();

    // ── Detalle por ID (sin BD, no disponible) ────────────────────
    public Task<BackupResponseDto> ObtenerBackupAsync(Guid id)
        => throw new NotFoundException("BackupJob", id);

    // ── Helpers privados ──────────────────────────────────────────

    private async Task<(string DriveId, string DriveEnlace)> SubirADriveAsync(
        string rutaArchivo, string nombreArchivo)
    {
        try
        {
            var driveId = await _driveService.SubirArchivoAsync(rutaArchivo, nombreArchivo);
            _logger.LogInformation("Backup subido a Google Drive. ID: {Id}", driveId);
            return (driveId, $"https://drive.google.com/file/d/{driveId}/view");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Drive error: {Msg}", ex.Message);
            return (string.Empty, ex.Message); // Retorna el error para mostrarlo al cliente
        }
    }

    private async Task EjecutarPgDumpAsync(string rutaArchivo, string? tabla)
    {
        // Credenciales: usa BACKUP_DB_USER/PASSWORD del .env, si no existe usa app_user
        var host     = Environment.GetEnvironmentVariable("DB_HOST")             ?? "localhost";
        var port     = Environment.GetEnvironmentVariable("DB_PORT")             ?? "5432";
        var database = Environment.GetEnvironmentVariable("DB_NAME")             ?? "floreria_bautista";
        var user     = Environment.GetEnvironmentVariable("BACKUP_DB_USER")      ?? Environment.GetEnvironmentVariable("DB_USER") ?? "app_user";
        var password = Environment.GetEnvironmentVariable("BACKUP_DB_PASSWORD")  ?? Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "";

        Directory.CreateDirectory(Path.GetDirectoryName(rutaArchivo)!);

        // Formato custom (-F c) igual que el script .bat — genera .backup
        var args = tabla is null
            ? $"-h {host} -p {port} -U {user} -F c -f \"{rutaArchivo}\" {database}"
            : $"-h {host} -p {port} -U {user} -F c --table={tabla} -f \"{rutaArchivo}\" {database}";

        var psi = new ProcessStartInfo
        {
            FileName               = "pg_dump",
            Arguments              = args,
            RedirectStandardError  = true,
            RedirectStandardOutput = false,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };

        // PGPASSWORD evita el prompt interactivo
        psi.Environment["PGPASSWORD"] = password;

        using var process = Process.Start(psi)
            ?? throw new AppException("No se pudo iniciar el proceso pg_dump.");

        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new AppException($"pg_dump falló (código {process.ExitCode}): {stderr}");
    }

    private static string GenerarRutaArchivo(string tipo, string? tabla)
    {
        var carpeta   = Environment.GetEnvironmentVariable("BACKUPS_RUTA_LOCAL") ?? "backups";
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
        var database  = Environment.GetEnvironmentVariable("DB_NAME") ?? "floreria_bautista";

        var nombre = tipo == "full"
            ? $"backup_{database}_full_{timestamp}.backup"
            : $"backup_{database}_{tabla}_{timestamp}.backup";

        return Path.Combine(carpeta, nombre);
    }

    private string BuildBackupConnectionString()
    {
        var host     = Environment.GetEnvironmentVariable("DB_HOST")            ?? "localhost";
        var port     = Environment.GetEnvironmentVariable("DB_PORT")            ?? "5432";
        var database = Environment.GetEnvironmentVariable("DB_NAME")            ?? "floreria_bautista";
        var user     = Environment.GetEnvironmentVariable("BACKUP_DB_USER")     ?? Environment.GetEnvironmentVariable("DB_USER") ?? "app_user";
        var password = Environment.GetEnvironmentVariable("BACKUP_DB_PASSWORD") ?? Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "";
        return $"Host={host};Port={port};Database={database};Username={user};Password={password}";
    }
}