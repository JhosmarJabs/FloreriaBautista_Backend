namespace FloreriaBautista.Models.DTOs.Recommendations;

public class ProductRecommendationDto
{
    public Guid    ProductId  { get; set; }
    public string  Nombre     { get; set; } = string.Empty;
    public decimal PrecioBase { get; set; }
    public string? ImagenUrl  { get; set; }

    // Afinidad acumulada con lo que hay en el carrito (suma de similitudes coseno).
    // Es null cuando la sugerencia viene del respaldo de más vendidos, no del modelo.
    public decimal? Afinidad  { get; set; }

    public bool    EsFallback { get; set; }

    // Explicación legible del porqué de la sugerencia. El §7 del proyecto exige
    // "indicar el significado del resultado", no solo mostrar la lista.
    public string  Motivo     { get; set; } = string.Empty;
}

// Estado del artefacto del recomendador. Sustituye al antiguo RecalcularReglasResultDto:
// las reglas de asociación ya no se usan (ver RecommendationService para el porqué).
public class RecomendadorEstadoDto
{
    public bool      Disponible     { get; set; }
    public string?   Configuracion  { get; set; }
    public string?   Scoring        { get; set; }
    public string?   VersionSklearn { get; set; }
    public int       NProductos     { get; set; }
    public string?   GeneradoEn     { get; set; }   // cuándo la libreta produjo el artefacto
    public DateTime? CargadoEn      { get; set; }   // cuándo el backend lo leyó de disco
    public string?   Error          { get; set; }
}
