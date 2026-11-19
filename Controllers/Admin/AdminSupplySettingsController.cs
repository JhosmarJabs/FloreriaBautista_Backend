using System.ComponentModel.DataAnnotations;
using System.Globalization;
using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Inventory;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace FloreriaBautista.Controllers.Admin;

[ApiController]
[Tags("2. Operaciones y Ventas")]
[Route("api/admin/supply-settings")]
[Authorize(Roles = "ADMIN")]
public class AdminSupplySettingsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;

    public AdminSupplySettingsController(AppDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache   = cache;
    }

    [HttpGet]
    public async Task<IActionResult> Obtener()
    {
        var s = await _context.SupplySettings.FindAsync(1) ?? new SupplySettings();
        return Ok(ApiResponseDto<SupplySettingsDto>.Ok(MapToDto(s)));
    }

    [HttpPut]
    public async Task<IActionResult> Actualizar([FromBody] ActualizarSupplySettingsRequest request)
    {
        // Defensa por si cambia la expresión regular del request: nunca 500 por entrada del cliente.
        if (!TimeOnly.TryParseExact(request.HoraCutoff, "HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var hora))
            throw new AppException("horaCutoff debe tener formato HH:mm (00:00 a 23:59).");

        var s = await _context.SupplySettings.FindAsync(1);
        if (s == null)
        {
            s = new SupplySettings { Id = 1 };
            _context.SupplySettings.Add(s);
        }

        s.DiaAbastecimiento         = request.DiaAbastecimiento!.Value;
        s.HoraCutoff                = hora;
        s.DiaCutoff                 = request.DiaCutoff!.Value;
        s.ToleranciaUnidades        = request.ToleranciaUnidades!.Value;
        s.ToleranciaPorcentaje      = request.ToleranciaPorcentaje!.Value;
        s.UsarToleranciaAbsoluta    = request.UsarToleranciaAbsoluta!.Value;
        s.MargenSeguridadPorcentaje = request.MargenSeguridadPorcentaje!.Value;
        s.DiasMinimosAnticipacion   = request.DiasMinimosAnticipacion!.Value;
        s.PermitirVentaSinReceta    = request.PermitirVentaSinReceta!.Value;

        await _context.SaveChangesAsync();
        _cache.Remove("supply-settings:v1");

        return Ok(ApiResponseDto<SupplySettingsDto>.Ok(MapToDto(s)));
    }

    private static SupplySettingsDto MapToDto(SupplySettings s) => new()
    {
        DiaAbastecimiento         = s.DiaAbastecimiento,
        HoraCutoff                = s.HoraCutoff.ToString("HH:mm"),
        DiaCutoff                 = s.DiaCutoff,
        ToleranciaUnidades        = s.ToleranciaUnidades,
        ToleranciaPorcentaje      = s.ToleranciaPorcentaje,
        UsarToleranciaAbsoluta    = s.UsarToleranciaAbsoluta,
        MargenSeguridadPorcentaje = s.MargenSeguridadPorcentaje,
        DiasMinimosAnticipacion   = s.DiasMinimosAnticipacion,
        PermitirVentaSinReceta    = s.PermitirVentaSinReceta
    };
}

/// <summary>
/// FIX-010: el PUT de supply-settings es reemplazo total. Los nueve campos son obligatorios y
/// van acotados; [ApiController] responde 400 con el campo identificado antes de entrar a la acción.
/// </summary>
public class ActualizarSupplySettingsRequest
{
    [Required(ErrorMessage = "diaAbastecimiento es obligatorio.")]
    [Range(0, 6, ErrorMessage = "diaAbastecimiento debe estar entre 0 (domingo) y 6 (sábado).")]
    public int? DiaAbastecimiento { get; set; }

    [Required(ErrorMessage = "horaCutoff es obligatoria.")]
    [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$",
        ErrorMessage = "horaCutoff debe tener formato HH:mm (00:00 a 23:59).")]
    public string? HoraCutoff { get; set; }

    [Required(ErrorMessage = "diaCutoff es obligatorio.")]
    [Range(0, 6, ErrorMessage = "diaCutoff debe estar entre 0 (domingo) y 6 (sábado).")]
    public int? DiaCutoff { get; set; }

    [Required(ErrorMessage = "toleranciaUnidades es obligatoria.")]
    [Range(0, int.MaxValue, ErrorMessage = "toleranciaUnidades debe ser mayor o igual a 0.")]
    public int? ToleranciaUnidades { get; set; }

    [Required(ErrorMessage = "toleranciaPorcentaje es obligatoria.")]
    [Range(0.0, 1.0, ErrorMessage = "toleranciaPorcentaje debe ser una fracción entre 0 y 1.")]
    public decimal? ToleranciaPorcentaje { get; set; }

    [Required(ErrorMessage = "usarToleranciaAbsoluta es obligatorio.")]
    public bool? UsarToleranciaAbsoluta { get; set; }

    [Required(ErrorMessage = "margenSeguridadPorcentaje es obligatorio.")]
    [Range(0.0, 1.0, ErrorMessage = "margenSeguridadPorcentaje debe ser una fracción entre 0 y 1.")]
    public decimal? MargenSeguridadPorcentaje { get; set; }

    [Required(ErrorMessage = "diasMinimosAnticipacion es obligatorio.")]
    [Range(0, int.MaxValue, ErrorMessage = "diasMinimosAnticipacion debe ser mayor o igual a 0.")]
    public int? DiasMinimosAnticipacion { get; set; }

    [Required(ErrorMessage = "permitirVentaSinReceta es obligatorio.")]
    public bool? PermitirVentaSinReceta { get; set; }
}
