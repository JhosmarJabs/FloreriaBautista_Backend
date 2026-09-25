namespace FloreriaBautista.Models.Entities;

public class Descuento
{
    public Guid     Id                { get; set; }
    public string   Nombre            { get; set; } = string.Empty;
    public string   TipoRegla         { get; set; } = "MONTO_MINIMO"; // MONTO_MINIMO | CATEGORIA | CATALOGO
    public decimal? MontoMinimoCompra { get; set; }
    public Guid?    CategoriaId       { get; set; }
    public Guid?    CatalogoId        { get; set; }
    public string   TipoValor         { get; set; } = "PORCENTAJE"; // PORCENTAJE | MONTO_FIJO
    public decimal  Valor             { get; set; }
    public DateOnly? FechaInicio      { get; set; }
    public DateOnly? FechaFin         { get; set; }
    public bool     Activo            { get; set; } = true;
    public DateTime CreadoEn          { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadoEn     { get; set; } = DateTime.UtcNow;

    public Category? Categoria { get; set; }
    public Catalogo? Catalogo  { get; set; }
}
