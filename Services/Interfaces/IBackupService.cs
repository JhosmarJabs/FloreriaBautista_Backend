using FloreriaBautista.Models.DTOs.Backups;

namespace FloreriaBautista.Services.Interfaces;

public interface IBackupService
{
    /// <summary>Devuelve la lista de tablas disponibles en la BD.</summary>
    Task<List<string>> ObtenerTablasAsync();

    /// <summary>Crea un respaldo completo (pg_dump) y lo sube a Drive.</summary>
    Task<BackupResponseDto> CrearBackupFullAsync(string? descripcion, Guid usuarioId, string formato = "BACKUP");

    /// <summary>Crea un respaldo de una tabla y lo sube a Drive.</summary>
    Task<BackupResponseDto> CrearBackupTablaAsync(string nombreTabla, string? descripcion, Guid usuarioId, string formato = "BACKUP");

    /// <summary>Lista todos los respaldos registrados en la BD.</summary>
    Task<List<BackupResponseDto>> ListarBackupsAsync();

    /// <summary>Lista los archivos de backup en Google Drive.</summary>
    Task<List<DriveFileDto>> ListarArchivosDriveAsync();

    /// <summary>Obtiene el detalle de un respaldo por su Id.</summary>
    Task<BackupResponseDto> ObtenerBackupAsync(Guid id);

    /// <summary>
    /// Elimina los backups más antiguos cuando la cantidad supera <paramref name="maxCopias"/>.
    /// Retorna el número de registros eliminados.
    /// </summary>
    Task<int> LimpiarBackupsAntiguosAsync(int maxCopias = 10);
}
