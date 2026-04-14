namespace FloreriaBautista.Models.Entities;

public class ProductRecipe
{
    public Guid Id                { get; set; }
    public Guid ProductId         { get; set; }
    public Guid InventoryItemId   { get; set; }
    public int  CantidadRequerida { get; set; }

    public Product       Product       { get; set; } = null!;
    public InventoryItem InventoryItem { get; set; } = null!;
}
