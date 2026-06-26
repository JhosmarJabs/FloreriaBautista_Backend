namespace FloreriaBautista.Models.Entities;

public class Catalogo
{
    public Guid     Id            { get; set; }
    public string   Nombre        { get; set; } = string.Empty;
    public string?  Descripcion   { get; set; }
    public string   Estado        { get; set; } = "ACTIVA";
    public bool     Activo        { get; set; } = true;
    public string?  ImagenUrl     { get; set; }
    public DateTime CreadoEn      { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;

    public ICollection<ProductCatalogo> ProductCatalogos { get; set; } = [];
}
