namespace FloreriaBautista.Models.DTOs.Cms;

public class HorarioDto
{
    public string Dia      { get; set; } = string.Empty;
    public string Apertura { get; set; } = "09:00";
    public string Cierre   { get; set; } = "19:00";
    public bool   Cerrado  { get; set; } = false;
}

public class SiteSettingsDto
{
    public string    NombreTienda    { get; set; } = string.Empty;
    public string    Telefono        { get; set; } = string.Empty;
    public string    Correo          { get; set; } = string.Empty;
    public string    Direccion       { get; set; } = string.Empty;
    public string    DireccionUrl    { get; set; } = string.Empty;
    public string    FacebookUrl     { get; set; } = string.Empty;
    public string    InstagramUrl    { get; set; } = string.Empty;
    public string    WhatsappUrl     { get; set; } = string.Empty;
    public string    BannerTitulo    { get; set; } = string.Empty;
    public string    BannerSubtitulo { get; set; } = string.Empty;
    public string    BannerCta       { get; set; } = string.Empty;
    public List<HorarioDto> Horarios { get; set; } = [];
    public List<string>     Destacados { get; set; } = [];
    public string    AnuncioTexto    { get; set; } = string.Empty;
    public bool      AnuncioActivo   { get; set; } = false;
}
