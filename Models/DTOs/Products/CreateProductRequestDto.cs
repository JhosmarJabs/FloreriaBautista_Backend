using System.ComponentModel.DataAnnotations;

namespace FloreriaBautista.Models.DTOs.Products;

public class CreateProductRequestDto
{
    [Required] public string  Nombre           { get; set; } = string.Empty;
    public string             Descripcion      { get; set; } = string.Empty;
    [Required] [Range(0.01, double.MaxValue)]
    public decimal            PrecioBase       { get; set; }
    [Required] public string  Tipo             { get; set; } = string.Empty;
    public bool               EsPersonalizable { get; set; } = false;
    public string             Estado           { get; set; } = "ACTIVO";
    public string?            ImagenUrl        { get; set; }
    public List<string>       Categorias       { get; set; } = [];
    public List<string>       Colecciones      { get; set; } = [];
}
