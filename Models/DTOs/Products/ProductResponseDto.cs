namespace FloreriaBautista.Models.DTOs.Products;

public class ProductResponseDto
{
    public Guid         Id               { get; set; }
    public string       Nombre           { get; set; } = string.Empty;
    public string       Descripcion      { get; set; } = string.Empty;
    public decimal      PrecioBase       { get; set; }
    public string       Tipo             { get; set; } = string.Empty;
    public bool         EsPersonalizable { get; set; }
    public string       Estado           { get; set; } = string.Empty;
    public string?      ImagenUrl        { get; set; }
    public List<string> Categorias       { get; set; } = [];
    public List<string> Colecciones      { get; set; } = [];
    public int?         StockActual      { get; set; }
    public DateTime     CreadoEn         { get; set; }
}
