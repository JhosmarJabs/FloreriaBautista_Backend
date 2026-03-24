using FloreriaBautista.Models.DTOs.Backups;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FloreriaBautista.Controllers;

[ApiController]
[Route("api/admin/backups")]
[Authorize(Roles = "ADMIN")]
public class AdminBackupsController : ControllerBase
{
    private readonly IBackupService _backupService;

    public AdminBackupsController(IBackupService backupService)
        => _backupService = backupService;

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

    // POST /api/admin/backups/full — backup completo, sube a Drive automáticamente
    [HttpPost("full")]
    public async Task<IActionResult> CrearFull([FromBody] BackupFullRequestDto request)
    {
        var usuarioId = ObtenerUsuarioId();
        if (usuarioId == null)
            return Unauthorized(ApiResponseDto<object>.Fail("No se pudo identificar al usuario."));

        var resultado = await _backupService.CrearBackupFullAsync(request?.Descripcion, usuarioId.Value, request?.Formato ?? "BACKUP");
        return Ok(ApiResponseDto<BackupResponseDto>.Ok(resultado));
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
        return Ok(ApiResponseDto<BackupResponseDto>.Ok(resultado));
    }

    private Guid? ObtenerUsuarioId()
    {
        var claim = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
