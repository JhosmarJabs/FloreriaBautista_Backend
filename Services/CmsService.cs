using System.Net.Mail;
using System.Text.Json;
using System.Text.RegularExpressions;
using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Cms;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Services;

public class CmsService
{
    // RFC de persona moral (3 letras) o física (4 letras) + fecha + homoclave.
    private static readonly Regex RfcRegex = new(@"^[A-ZÑ&]{3,4}\d{6}[A-Z0-9]{3}$", RegexOptions.Compiled);

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

        // Datos legales: null = el campo no viene y se conserva; "" = el admin lo vació y se guarda null.
        var rfc              = NormalizarOpcional(request.Rfc, settings.Rfc, upper: true);
        var correoPrivacidad = NormalizarOpcional(request.CorreoPrivacidad, settings.CorreoPrivacidad, upper: false);
        var razonSocial      = NormalizarOpcional(request.RazonSocial, settings.RazonSocial, upper: false);
        if (rfc is not null && !RfcRegex.IsMatch(rfc))
            throw new AppException("El RFC no tiene un formato válido (por ejemplo, BAUJ800101AB1).");
        if (correoPrivacidad is not null && !EsCorreoValido(correoPrivacidad))
            throw new AppException("El correo para privacidad no tiene un formato válido.");
        if (razonSocial is { Length: > 200 })
            throw new AppException("La razón social no puede pasar de 200 caracteres.");
        if (correoPrivacidad is { Length: > 150 })
            throw new AppException("El correo para privacidad no puede pasar de 150 caracteres.");

        settings.RazonSocial      = razonSocial;
        settings.Rfc              = rfc;
        settings.CorreoPrivacidad = correoPrivacidad;
        settings.NombreTienda    = request.NombreTienda ?? settings.NombreTienda;
        settings.Telefono        = request.Telefono ?? settings.Telefono;
        settings.Correo          = request.Correo ?? settings.Correo;
        settings.Direccion       = request.Direccion ?? settings.Direccion;
        settings.DireccionUrl    = request.DireccionUrl ?? settings.DireccionUrl;
        settings.FacebookUrl     = request.FacebookUrl ?? settings.FacebookUrl;
        settings.InstagramUrl    = request.InstagramUrl ?? settings.InstagramUrl;
        settings.WhatsappUrl     = request.WhatsappUrl ?? settings.WhatsappUrl;
        settings.BannerTitulo    = request.BannerTitulo ?? settings.BannerTitulo;
        settings.BannerSubtitulo = request.BannerSubtitulo ?? settings.BannerSubtitulo;
        settings.BannerCta       = request.BannerCta ?? settings.BannerCta;
        settings.HorariosJson    = JsonSerializer.Serialize(request.Horarios ?? []);
        settings.DestacadosJson  = JsonSerializer.Serialize(request.Destacados ?? []);
        settings.Latitud         = request.Latitud ?? settings.Latitud;
        settings.Longitud        = request.Longitud ?? settings.Longitud;
        settings.AnuncioTexto    = request.AnuncioTexto ?? settings.AnuncioTexto;
        settings.AnuncioActivo   = request.AnuncioActivo;
        settings.ActualizadoEn   = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return MapToDto(settings);
    }

    private static string? NormalizarOpcional(string? entrante, string? actual, bool upper)
    {
        if (entrante is null) return actual;
        var limpio = entrante.Trim();
        if (limpio.Length == 0) return null;
        return upper ? limpio.ToUpperInvariant() : limpio;
    }

    private static bool EsCorreoValido(string correo)
    {
        if (!MailAddress.TryCreate(correo, out var dir)) return false;
        return dir.Address == correo && dir.Host.Contains('.');
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
        DireccionUrl    = s.DireccionUrl,
        RazonSocial      = s.RazonSocial,
        Rfc              = s.Rfc,
        CorreoPrivacidad = s.CorreoPrivacidad,
        FacebookUrl     = s.FacebookUrl,
        InstagramUrl    = s.InstagramUrl,
        WhatsappUrl     = s.WhatsappUrl,
        BannerTitulo    = s.BannerTitulo,
        BannerSubtitulo = s.BannerSubtitulo,
        BannerCta       = s.BannerCta,
        Horarios        = string.IsNullOrWhiteSpace(s.HorariosJson) ? [] : JsonSerializer.Deserialize<List<HorarioDto>>(s.HorariosJson) ?? [],
        Destacados      = string.IsNullOrWhiteSpace(s.DestacadosJson) ? [] : JsonSerializer.Deserialize<List<string>>(s.DestacadosJson) ?? [],
        Latitud         = s.Latitud,
        Longitud        = s.Longitud,
        AnuncioTexto    = s.AnuncioTexto,
        AnuncioActivo   = s.AnuncioActivo
    };
}
