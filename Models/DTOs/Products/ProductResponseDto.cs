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
    public string       Visibilidad      { get; set; } = string.Empty;
    public string?      ImagenUrl        { get; set; }
    public bool         Activo           { get; set; }
    public List<string> Categorias       { get; set; } = [];
    public List<string> Catalogos       { get; set; } = [];
    public List<RecipeItemDto> Receta    { get; set; } = [];
    public DateTime     CreadoEn         { get; set; }
    public DateTime?    ActualizadoEn    { get; set; }
}

public class RecipeItemDto
{
    public Guid    InventoryItemId { get; set; }
    public string  Nombre          { get; set; } = string.Empty;
    public int     Cantidad        { get; set; }
    public decimal PrecioCosto     { get; set; }
    public bool    EsFlorPrimaria  { get; set; }
}
