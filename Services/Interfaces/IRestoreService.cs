using FloreriaBautista.Models.DTOs.Backups;
using FloreriaBautista.Models.DTOs.Database;

namespace FloreriaBautista.Services.Interfaces;

public interface IRestoreService
{
    /// <summary>
    /// Descarga un archivo de Google Drive y lo restaura con psql.
    /// Requiere confirmación explícita en el request.
    /// </summary>
    Task<RestoreResponseDto> RestaurarDesdeDriveAsync(RestoreRequestDto request);

    /// <summary>Lista los archivos de backup disponibles en Drive para restaurar.</summary>
    Task<List<DriveFileDto>> ListarBackupsDriveAsync();
}
