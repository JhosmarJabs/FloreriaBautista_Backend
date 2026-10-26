using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Inventory;
using FloreriaBautista.Models.Entities;
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
    public async Task<IActionResult> Actualizar([FromBody] SupplySettingsDto dto)
    {
        var s = await _context.SupplySettings.FindAsync(1);
        if (s == null)
        {
            s = new SupplySettings { Id = 1 };
            _context.SupplySettings.Add(s);
        }

        s.DiaAbastecimiento         = dto.DiaAbastecimiento;
        s.HoraCutoff                = TimeOnly.Parse(dto.HoraCutoff);
        s.DiaCutoff                 = dto.DiaCutoff;
        s.ToleranciaUnidades        = dto.ToleranciaUnidades;
        s.ToleranciaPorcentaje      = dto.ToleranciaPorcentaje;
        s.UsarToleranciaAbsoluta    = dto.UsarToleranciaAbsoluta;
        s.MargenSeguridadPorcentaje = dto.MargenSeguridadPorcentaje;
        s.DiasMinimosAnticipacion   = dto.DiasMinimosAnticipacion;
        s.PermitirVentaSinReceta    = dto.PermitirVentaSinReceta;

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
