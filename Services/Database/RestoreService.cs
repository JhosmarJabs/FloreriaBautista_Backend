using System.Diagnostics;
using FloreriaBautista.Models.DTOs.Backups;
using FloreriaBautista.Models.DTOs.Database;
using FloreriaBautista.Services.Backups;
using FloreriaBautista.Services.Interfaces;

namespace FloreriaBautista.Services.Database;

public class RestoreService : IRestoreService
{
    private readonly GoogleDriveService      _driveService;
    private readonly ILogger<RestoreService> _logger;

    private const string ConfirmacionRequerida = "CONFIRMAR_RESTAURACION";

    public RestoreService(
        GoogleDriveService driveService,
        ILogger<RestoreService> logger)
    {
        _driveService = driveService;
        _logger       = logger;
    }

    public async Task<List<DriveFileDto>> ListarBackupsDriveAsync()
        => await _driveService.ListarArchivosAsync();

    public async Task<RestoreResponseDto> RestaurarDesdeDriveAsync(RestoreRequestDto request)
    {
        var sw  = Stopwatch.StartNew();
        var dto = new RestoreResponseDto { EjecutadoEn = DateTime.UtcNow };

        if (request.Confirmacion != ConfirmacionRequerida)
        {
            dto.Estado       = "ERROR";
            dto.MensajeError = $"Para restaurar debes enviar Confirmacion = \"{ConfirmacionRequerida}\".";
            return dto;
        }

        var carpetaTemp      = Path.Combine(Path.GetTempPath(), "floreria_restore");
        var rutaArchivoLocal = string.Empty;

        try
        {
            Directory.CreateDirectory(carpetaTemp);

            _logger.LogInformation("Descargando backup desde Drive. FileId: {Id}", request.DriveFileId);
            rutaArchivoLocal = await _driveService.DescargarArchivoAsync(request.DriveFileId, carpetaTemp);

            dto.ArchivoRestaurado = Path.GetFileName(rutaArchivoLocal);
            _logger.LogInformation("Archivo descargado: {Ruta}", rutaArchivoLocal);

            await EjecutarRestoreAsync(rutaArchivoLocal, request.LimpiarAntes);

            sw.Stop();
            dto.Estado     = "COMPLETADO";
            dto.DuracionMs = sw.Elapsed.TotalMilliseconds;
            _logger.LogInformation("Restauración completada en {Ms} ms", dto.DuracionMs);
        }
        catch (Exception ex)
        {
            sw.Stop();
            dto.Estado       = "ERROR";
            dto.MensajeError = ex.Message;
            dto.DuracionMs   = sw.Elapsed.TotalMilliseconds;
            _logger.LogError(ex, "Error en restauración");
        }
        finally
        {
            if (!string.IsNullOrEmpty(rutaArchivoLocal) && File.Exists(rutaArchivoLocal))
            {
                File.Delete(rutaArchivoLocal);
                _logger.LogInformation("Archivo temporal eliminado: {Ruta}", rutaArchivoLocal);
            }
        }

        return dto;
    }

    // ── Helpers ───────────────────────────────────────────────────

    private async Task EjecutarRestoreAsync(string rutaArchivo, bool limpiarAntes)
    {
        var host     = Env("DB_HOST");
        var port     = Env("DB_PORT");
        var database = Env("DB_NAME");
        // Restaurar requiere ser dueño del schema — usa db_admin_user
        var user     = Env("DB_ADMIN_USER");
        var password = Env("DB_ADMIN_PASSWORD");

        if (limpiarAntes)
        {
            _logger.LogWarning("LimpiarAntes = true: se recreará el schema public.");
            await EjecutarPsqlAsync(
                $"-h {host} -p {port} -U {user} -d {database} -c \"DROP SCHEMA public CASCADE; CREATE SCHEMA public;\"",
                password);
        }

        // pg_restore para formato custom (.backup), psql para plain (.sql)
        var esFormatoCustom = rutaArchivo.EndsWith(".backup", StringComparison.OrdinalIgnoreCase);

        if (esFormatoCustom)
        {
            var args = $"-h {host} -p {port} -U {user} -d {database} --no-owner --role={user} \"{rutaArchivo}\"";
            await EjecutarProcesoAsync("pg_restore", args, password);
        }
        else
        {
            var args = $"-h {host} -p {port} -U {user} -d {database} -f \"{rutaArchivo}\"";
            await EjecutarPsqlAsync(args, password);
        }
    }

    private async Task EjecutarPsqlAsync(string args, string password)
        => await EjecutarProcesoAsync("psql", args, password);

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

    private static string Env(string key) =>
        Environment.GetEnvironmentVariable(key)
        ?? throw new InvalidOperationException($"Variable '{key}' no configurada en .env");
}
