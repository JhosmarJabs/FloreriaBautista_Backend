using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.ImportExport;
using FloreriaBautista.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FloreriaBautista.Controllers;

[ApiController]
[Authorize(Roles = "ADMIN")]
public class AdminImportExportController : ControllerBase
{
    private readonly IExportService _exportService;
    private readonly IImportService _importService;

    public AdminImportExportController(IExportService exportService, IImportService importService)
    {
        _exportService = exportService;
        _importService = importService;
    }

    // GET /api/admin/export/products
    [HttpGet("api/admin/export/products")]
    public async Task<IActionResult> ExportarProductos()
    {
        var (contenido, nombre) = await _exportService.ExportarProductosAsync();
        return File(contenido, "text/csv; charset=utf-8", nombre);
    }

    // GET /api/admin/export/inventory
    [HttpGet("api/admin/export/inventory")]
    public async Task<IActionResult> ExportarInventario()
    {
        var (contenido, nombre) = await _exportService.ExportarInventarioAsync();
        return File(contenido, "text/csv; charset=utf-8", nombre);
    }

    // POST /api/admin/import/products
    [HttpPost("api/admin/import/products")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB máximo
    public async Task<IActionResult> ImportarProductos(IFormFile archivo)
    {
        if (archivo == null || archivo.Length == 0)
            return BadRequest(ApiResponseDto<object>.Fail("No se recibió ningún archivo."));

        if (!archivo.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponseDto<object>.Fail("Solo se aceptan archivos .csv"));

        await using var stream = archivo.OpenReadStream();
        var resultado = await _importService.ImportarProductosAsync(stream, archivo.FileName);

        return Ok(ApiResponseDto<ImportResultDto>.Ok(resultado,
            $"Importación completada: {resultado.Insertados} insertados, {resultado.Actualizados} actualizados, {resultado.Errores} errores."));
    }

    // POST /api/admin/import/inventory
    [HttpPost("api/admin/import/inventory")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> ImportarInventario(IFormFile archivo)
    {
        if (archivo == null || archivo.Length == 0)
            return BadRequest(ApiResponseDto<object>.Fail("No se recibió ningún archivo."));

        if (!archivo.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponseDto<object>.Fail("Solo se aceptan archivos .csv"));

        await using var stream = archivo.OpenReadStream();
        var resultado = await _importService.ImportarInventarioAsync(stream, archivo.FileName);

        return Ok(ApiResponseDto<ImportResultDto>.Ok(resultado,
            $"Importación completada: {resultado.Insertados} insertados, {resultado.Actualizados} actualizados, {resultado.Errores} errores."));
    }

    // GET /api/admin/export/flowers
    [HttpGet("api/admin/export/flowers")]
    public async Task<IActionResult> ExportarFlores()
    {
        var (contenido, nombre) = await _exportService.ExportarFloresAsync();
        return File(contenido, "text/csv; charset=utf-8", nombre);
    }

    // POST /api/admin/import/flowers
    [HttpPost("api/admin/import/flowers")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> ImportarFlores(IFormFile archivo)
    {
        if (archivo == null || archivo.Length == 0)
            return BadRequest(ApiResponseDto<object>.Fail("No se recibió ningún archivo."));

        if (!archivo.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponseDto<object>.Fail("Solo se aceptan archivos .csv"));

        await using var stream = archivo.OpenReadStream();
        var resultado = await _importService.ImportarFloresAsync(stream, archivo.FileName);

        return Ok(ApiResponseDto<ImportResultDto>.Ok(resultado,
            $"Importación completada: {resultado.Insertados} insertadas, {resultado.Actualizados} actualizadas, {resultado.Errores} errores."));
    }
}
