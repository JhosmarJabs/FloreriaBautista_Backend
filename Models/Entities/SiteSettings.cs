namespace FloreriaBautista.Models.Entities;

public class SiteSettings
{
    public int     Id              { get; set; } = 1;

    // ── Información de contacto ─────────────────────────────────────
    public string  NombreTienda    { get; set; } = "Florería Bautista";
    public string  Telefono        { get; set; } = string.Empty;
    public string  Correo          { get; set; } = string.Empty;
    public string  Direccion       { get; set; } = string.Empty;

    // ── Banner del hero (uno activo a la vez) ────────────────────────
    public string  BannerTitulo    { get; set; } = string.Empty;
    public string  BannerSubtitulo { get; set; } = string.Empty;
    public string  BannerCta       { get; set; } = "Ver catálogo";

    // ── Horarios de atención (JSON: [{dia,apertura,cierre,cerrado}]) ─
    public string  HorariosJson    { get; set; } = "[]";

    // ── Productos destacados (JSON: ["nombre1","nombre2"]) ───────────
    public string  DestacadosJson  { get; set; } = "[]";

    // ── Anuncio global ────────────────────────────────────────────────
    public string  AnuncioTexto    { get; set; } = string.Empty;
    public bool    AnuncioActivo   { get; set; } = false;

    public DateTime ActualizadoEn  { get; set; } = DateTime.UtcNow;
}
