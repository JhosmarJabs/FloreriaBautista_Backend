using System.Security.Claims;
using FloreriaBautista.Controllers.Admin;
using FloreriaBautista.Models.DTOs.Backups;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FloreriaBautista.Tests;

/// <summary>FIX-003 (H-001): el controlador de respaldos no debe responder success = true si el respaldo terminó en ERROR.</summary>
public class AdminBackupsRespuestaTests
{
    private sealed class ServicioFalso : IBackupService
    {
        public BackupResponseDto Resultado = new();
        public Task<List<string>> ObtenerTablasAsync() => Task.FromResult(new List<string>());
        public Task<BackupResponseDto> CrearBackupFullAsync(string? d, Guid u, string f = "BACKUP", string dest = "DRIVE") => Task.FromResult(Resultado);
        public Task<BackupResponseDto> CrearBackupTablaAsync(string t, string? d, Guid u, string f = "BACKUP") => Task.FromResult(Resultado);
        public Task<List<BackupResponseDto>> ListarBackupsAsync() => Task.FromResult(new List<BackupResponseDto>());
        public Task<List<DriveFileDto>> ListarArchivosDriveAsync() => Task.FromResult(new List<DriveFileDto>());
        public Task<BackupResponseDto> ObtenerBackupAsync(Guid id) => Task.FromResult(Resultado);
        public Task<int> LimpiarBackupsAntiguosAsync(int maxCopias = 10) => Task.FromResult(0);
    }

    private sealed class AuditoriaFalsa : IAuditService
    {
        public Task RegistrarAsync(string accion, string entidad, string? entidadId, Guid? usuarioId, object? detalles = null) => Task.CompletedTask;
    }

    private static AdminBackupsController Crear(BackupResponseDto resultado)
    {
        var c = new AdminBackupsController(new ServicioFalso { Resultado = resultado }, new AuditoriaFalsa());
        var identidad = new ClaimsIdentity(new[] { new Claim("sub", Guid.NewGuid().ToString()) }, "test");
        c.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identidad) } };
        return c;
    }

    private static ApiResponseDto<BackupResponseDto> Cuerpo(IActionResult r)
    {
        var ok = Assert.IsType<OkObjectResult>(r);
        Assert.Equal(200, ok.StatusCode ?? 200);
        return Assert.IsType<ApiResponseDto<BackupResponseDto>>(ok.Value);
    }

    [Fact]
    public async Task Full_En_Error_Responde_SuccessFalse_Con_Motivo_Y_Data()
    {
        var c = Crear(new BackupResponseDto { Id = Guid.NewGuid(), Estado = "ERROR", MensajeError = "Error al subir a Cloudinary: Invalid api_key [REDACTADO]" });

        var b = Cuerpo(await c.CrearFull(new BackupFullRequestDto()));

        Assert.False(b.Success);
        Assert.Equal("El respaldo falló: Error al subir a Cloudinary: Invalid api_key [REDACTADO]", b.Message);
        Assert.Equal("ERROR", b.Data!.Estado);
    }

    [Fact]
    public async Task Full_En_Error_Sin_Motivo_Usa_Mensaje_Generico()
    {
        var c = Crear(new BackupResponseDto { Id = Guid.NewGuid(), Estado = "ERROR", MensajeError = null });

        var b = Cuerpo(await c.CrearFull(new BackupFullRequestDto()));

        Assert.False(b.Success);
        Assert.Equal("El respaldo falló.", b.Message);
    }

    [Fact]
    public async Task Tabla_En_Error_Responde_SuccessFalse()
    {
        var c = Crear(new BackupResponseDto { Id = Guid.NewGuid(), Estado = "ERROR", MensajeError = "Drive sin token" });

        var b = Cuerpo(await c.CrearTabla(new BackupTablaRequestDto { NombreTabla = "productos" }));

        Assert.False(b.Success);
        Assert.Equal("El respaldo falló: Drive sin token", b.Message);
        Assert.NotNull(b.Data);
    }

    [Fact]
    public async Task Completado_Sigue_Con_SuccessTrue_Y_Mensaje_Vacio()
    {
        var c = Crear(new BackupResponseDto { Id = Guid.NewGuid(), Estado = "COMPLETADO" });

        var f = Cuerpo(await c.CrearFull(new BackupFullRequestDto()));
        var t = Cuerpo(await c.CrearTabla(new BackupTablaRequestDto { NombreTabla = "productos" }));

        Assert.True(f.Success);
        Assert.Equal(string.Empty, f.Message);
        Assert.True(t.Success);
        Assert.Equal(string.Empty, t.Message);
    }
}
