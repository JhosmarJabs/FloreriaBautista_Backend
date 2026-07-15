namespace FloreriaBautista.Models.Entities;

public class Product
{
    public Guid     Id               { get; set; }
    public string   Nombre           { get; set; } = string.Empty;
    public string   Descripcion      { get; set; } = string.Empty;
    public decimal  PrecioBase       { get; set; }
    public string   Tipo             { get; set; } = string.Empty;
    public bool     EsPersonalizable { get; set; } = false;
    public string   Estado           { get; set; } = "ACTIVO";
    public string   Visibilidad      { get; set; } = "AMBOS"; // WEB / SOLO_SUCURSAL / AMBOS
    public string?  ImagenUrl        { get; set; }
    public bool     Activo           { get; set; } = true;
    public DateTime CreadoEn         { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadoEn    { get; set; } = DateTime.UtcNow;

    public ICollection<ProductCategory>            ProductCategories            { get; set; } = [];
    public ICollection<ProductCatalogo>            ProductCatalogos             { get; set; } = [];
    public ICollection<ProductCustomizationOption> ProductCustomizationOptions  { get; set; } = [];
    public ICollection<OrderItem>                  OrderItems                   { get; set; } = [];
    public ICollection<ProductRecipe>              ProductRecipes               { get; set; } = [];
    public ICollection<QuickSaleTemplateItem>      QuickSaleTemplateItems       { get; set; } = [];
}
