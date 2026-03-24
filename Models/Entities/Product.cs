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
    public string?  ImagenUrl        { get; set; }
    public decimal  CostoBase        { get; set; } = 0;
    public decimal  MargenFactor     { get; set; } = 2.5m;
    public decimal  PrecioSugerido   { get; set; } = 0;
    public DateTime CreadoEn        { get; set; } = DateTime.UtcNow;

    public ICollection<ProductCategory>            ProductCategories            { get; set; } = [];
    public ICollection<ProductCollection>          ProductCollections           { get; set; } = [];
    public ICollection<ProductCustomizationOption> ProductCustomizationOptions  { get; set; } = [];
    public ICollection<OrderItem>                  OrderItems                   { get; set; } = [];
    public InventoryItem?                          InventoryItem                { get; set; }
    public ICollection<ProductFlower>              ProductFlowers               { get; set; } = [];
}
