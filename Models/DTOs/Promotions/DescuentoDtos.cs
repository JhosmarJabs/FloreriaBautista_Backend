namespace FloreriaBautista.Models.DTOs.Promotions;

public class DescuentoDto
{
    public Guid      Id                { get; set; }
    public string    Nombre            { get; set; } = string.Empty;
    public string    TipoRegla         { get; set; } = string.Empty;
    public decimal?  MontoMinimoCompra { get; set; }
    public Guid?     CategoriaId       { get; set; }
    public string?   CategoriaNombre   { get; set; }
    public Guid?     CatalogoId        { get; set; }
    public string?   CatalogoNombre    { get; set; }
    public string    TipoValor         { get; set; } = string.Empty;
    public decimal   Valor             { get; set; }
    public DateOnly? FechaInicio       { get; set; }
    public DateOnly? FechaFin          { get; set; }
    public bool      Activo            { get; set; }
    public DateTime  CreadoEn          { get; set; }
}

public class SaveDescuentoRequestDto
{
    public string    Nombre            { get; set; } = string.Empty;
    public string    TipoRegla         { get; set; } = "MONTO_MINIMO";
    public decimal?  MontoMinimoCompra { get; set; }
    public Guid?     CategoriaId       { get; set; }
    public Guid?     CatalogoId        { get; set; }
    public string    TipoValor         { get; set; } = "PORCENTAJE";
    public decimal   Valor             { get; set; }
    public DateOnly? FechaInicio       { get; set; }
    public DateOnly? FechaFin          { get; set; }
    public bool      Activo            { get; set; } = true;
}

public class DescuentoPublicoDto
{
    public Guid     Id        { get; set; }
    public string   Nombre    { get; set; } = string.Empty;
    public string   TipoRegla { get; set; } = string.Empty;
    public decimal? MontoMinimoCompra { get; set; }
    public string?  CategoriaNombre   { get; set; }
    public string?  CatalogoNombre    { get; set; }
    public string   TipoValor { get; set; } = string.Empty;
    public decimal  Valor     { get; set; }
    public DateOnly? FechaFin { get; set; }
}
