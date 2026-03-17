namespace FloreriaBautista.Models.DTOs.Products;

public class UpdateProductRequestDto
{
    public string?      Nombre           { get; set; }
    public string?      Descripcion      { get; set; }
    public decimal?     PrecioBase       { get; set; }
    public string?      Tipo             { get; set; }
    public bool?        EsPersonalizable { get; set; }
    public string?      Estado           { get; set; }
    public string?      ImagenUrl        { get; set; }
    public List<string>? Categorias      { get; set; }
    public List<string>? Colecciones     { get; set; }
}
