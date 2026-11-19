using FloreriaBautista.Models.DTOs.Backups;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FloreriaBautista.PlayReview;

namespace FloreriaBautista.Controllers.Admin;

[ApiController]
[Tags("3. Sistema y Seguridad")]
[Route("api/admin/backups")]
[Authorize(Roles = "ADMIN")]
[BloqueadoEnRevision] // play_review: 403 en la instancia de revisión
public class AdminBackupsController : ControllerBase
{
    private readonly IBackupService  _backupService;
    private readonly IAuditService   _audit;

    public AdminBackupsController(IBackupService backupService, IAuditService audit)
    {
        _backupService = backupService;
        _audit         = audit;
    }

    // GET /api/admin/backups — lista archivos .backup en carpeta local
    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var backups = await _backupService.ListarBackupsAsync();
        return Ok(ApiResponseDto<List<BackupResponseDto>>.Ok(backups));
    }

    // GET /api/admin/backups/drive — lista backups en Google Drive
    [HttpGet("drive")]
    public async Task<IActionResult> ListarDrive()
    {
        try
        {
            var archivos = await _backupService.ListarArchivosDriveAsync();
            return Ok(ApiResponseDto<List<DriveFileDto>>.Ok(archivos));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponseDto<List<DriveFileDto>>.Ok(
                [], $"Google Drive no disponible: {ex.Message}"));
        }
    }

    // GET /api/admin/backups/tablas — lista tablas disponibles para backup parcial
    [HttpGet("tablas")]
    public async Task<IActionResult> ListarTablas()
    {
        var tablas = await _backupService.ObtenerTablasAsync();
        return Ok(ApiResponseDto<List<string>>.Ok(tablas));
    }

    // POST /api/admin/backups/full — backup completo, sube a Drive automáticamente
    [HttpPost("full")]
    public async Task<IActionResult> CrearFull([FromBody] BackupFullRequestDto request)
    {
        var usuarioId = ObtenerUsuarioId();
        if (usuarioId == null)
            return Unauthorized(ApiResponseDto<object>.Fail("No se pudo identificar al usuario."));

        var resultado = await _backupService.CrearBackupFullAsync(
            request?.Descripcion, usuarioId.Value, request?.Formato ?? "BACKUP", request?.Destino ?? "DRIVE");

        await _audit.RegistrarAsync("BACKUP_FULL", "BackupJob", resultado.Id.ToString(), usuarioId,
            new { resultado.RutaArchivoLocal, resultado.Estado, resultado.SubidoADrive, resultado.Descripcion });

        return Ok(RespuestaDeRespaldo(resultado));
    }

    // POST /api/admin/backups/tabla — backup de tabla específica, sube a Drive automáticamente
    [HttpPost("tabla")]
    public async Task<IActionResult> CrearTabla([FromBody] BackupTablaRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request?.NombreTabla))
            return BadRequest(ApiResponseDto<object>.Fail("Debes indicar 'nombreTabla'."));

        var usuarioId = ObtenerUsuarioId();
        if (usuarioId == null)
            return Unauthorized(ApiResponseDto<object>.Fail("No se pudo identificar al usuario."));

        var resultado = await _backupService.CrearBackupTablaAsync(
            request.NombreTabla, request.Descripcion, usuarioId.Value, request.Formato);

        await _audit.RegistrarAsync("BACKUP_TABLA", "BackupJob", resultado.Id.ToString(), usuarioId,
            new { resultado.RutaArchivoLocal, resultado.Estado, resultado.SubidoADrive, Tabla = request.NombreTabla, resultado.Descripcion });

        return Ok(RespuestaDeRespaldo(resultado));
    }

    // Un respaldo fallido no lanza excepción en el servicio: se devuelve con Estado = ERROR.
    // Se responde HTTP 200 con success = false para que el panel muestre el motivo (MensajeError ya viene saneado).
    private static ApiResponseDto<BackupResponseDto> RespuestaDeRespaldo(BackupResponseDto resultado)
    {
        if (!string.Equals(resultado.Estado, "ERROR", StringComparison.OrdinalIgnoreCase))
            return ApiResponseDto<BackupResponseDto>.Ok(resultado);

        return new ApiResponseDto<BackupResponseDto>
        {
            Success = false,
            Message = string.IsNullOrWhiteSpace(resultado.MensajeError)
                ? "El respaldo falló."
                : $"El respaldo falló: {resultado.MensajeError}",
            Data = resultado
        };
    }

    private Guid? ObtenerUsuarioId()
    {
        var claim = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
