using System.Text.Json;
using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Cms;
using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Services;

public class CmsService
{
    private readonly AppDbContext _context;
    public CmsService(AppDbContext context) => _context = context;

    public async Task<SiteSettingsDto> ObtenerAsync()
    {
        var settings = await _context.SiteSettings.FindAsync(1) ?? await CrearPorDefectoAsync();
        return MapToDto(settings);
    }

    public async Task<SiteSettingsDto> ActualizarAsync(SiteSettingsDto request)
    {
        var settings = await _context.SiteSettings.FindAsync(1) ?? await CrearPorDefectoAsync();

        settings.NombreTienda    = request.NombreTienda ?? settings.NombreTienda;
        settings.Telefono        = request.Telefono ?? settings.Telefono;
        settings.Correo          = request.Correo ?? settings.Correo;
        settings.Direccion       = request.Direccion ?? settings.Direccion;
        settings.BannerTitulo    = request.BannerTitulo ?? settings.BannerTitulo;
        settings.BannerSubtitulo = request.BannerSubtitulo ?? settings.BannerSubtitulo;
        settings.BannerCta       = request.BannerCta ?? settings.BannerCta;
        settings.HorariosJson    = JsonSerializer.Serialize(request.Horarios ?? []);
        settings.DestacadosJson  = JsonSerializer.Serialize(request.Destacados ?? []);
        settings.AnuncioTexto    = request.AnuncioTexto ?? settings.AnuncioTexto;
        settings.AnuncioActivo   = request.AnuncioActivo;
        settings.ActualizadoEn   = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return MapToDto(settings);
    }

    private async Task<SiteSettings> CrearPorDefectoAsync()
    {
        var settings = new SiteSettings { Id = 1 };
        _context.SiteSettings.Add(settings);
        await _context.SaveChangesAsync();
        return settings;
    }

    private static SiteSettingsDto MapToDto(SiteSettings s) => new()
    {
        NombreTienda    = s.NombreTienda,
        Telefono        = s.Telefono,
        Correo          = s.Correo,
        Direccion       = s.Direccion,
        BannerTitulo    = s.BannerTitulo,
        BannerSubtitulo = s.BannerSubtitulo,
        BannerCta       = s.BannerCta,
        Horarios        = string.IsNullOrWhiteSpace(s.HorariosJson) ? [] : JsonSerializer.Deserialize<List<HorarioDto>>(s.HorariosJson) ?? [],
        Destacados      = string.IsNullOrWhiteSpace(s.DestacadosJson) ? [] : JsonSerializer.Deserialize<List<string>>(s.DestacadosJson) ?? [],
        AnuncioTexto    = s.AnuncioTexto,
        AnuncioActivo   = s.AnuncioActivo
    };
}
