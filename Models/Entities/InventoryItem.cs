namespace FloreriaBautista.Models.Entities;

public class InventoryItem
{
    public Guid    Id            { get; set; }
    public string  Nombre        { get; set; } = string.Empty;
    public int     StockActual   { get; set; } = 0;
    public int     StockMinimo   { get; set; } = 0;
    public string  Sucursal      { get; set; } = string.Empty;
    public decimal PrecioCosto   { get; set; } = 0;
    public bool    EsFlorPrimaria { get; set; } = false;
    public bool    SumaAlCosto   { get; set; } = true;
    public string? UnidadMedida  { get; set; }
    public string? ImagenUrl     { get; set; }
    public bool    Activo        { get; set; } = true;

    public ICollection<InventoryMovement> InventoryMovements { get; set; } = [];
    public ICollection<ProductRecipe>     ProductRecipes     { get; set; } = [];
}
