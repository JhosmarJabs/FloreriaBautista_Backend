namespace FloreriaBautista.Models.DTOs.Products;

public class ProductIndexDto
{
    public Guid     Id            { get; set; }
    public string   Nombre        { get; set; } = string.Empty;
    public decimal  PrecioBase    { get; set; }
    public string   Tipo          { get; set; } = string.Empty;
    public string   Estado        { get; set; } = string.Empty;
    public string?  ImagenUrl     { get; set; }
    public bool     Activo        { get; set; }
    public bool     EsReal        { get; set; }
    public DateTime ActualizadoEn { get; set; }
}
