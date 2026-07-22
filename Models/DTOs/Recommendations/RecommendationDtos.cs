namespace FloreriaBautista.Models.DTOs.Recommendations;

public class ProductRecommendationDto
{
    public Guid    ProductId  { get; set; }
    public string  Nombre     { get; set; } = string.Empty;
    public decimal PrecioBase { get; set; }
    public string? ImagenUrl  { get; set; }
    public decimal? Confianza { get; set; } // null cuando viene del fallback (top ventas), no de una regla
    public bool    EsFallback { get; set; }
}

public class RecalcularReglasResultDto
{
    public int ReglasGeneradas   { get; set; }
    public int TransaccionesUsadas { get; set; }
    public DateTime CalculadoEn   { get; set; }
}
