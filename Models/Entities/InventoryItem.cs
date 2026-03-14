namespace FloreriaBautista.Models.Entities;

public class InventoryItem
{
    public Guid    Id          { get; set; }
    public Guid    ProductId   { get; set; }
    public int     StockActual { get; set; } = 0;
    public int     StockMinimo { get; set; } = 0;
    public string  Sucursal    { get; set; } = string.Empty;

    public Product                       Product              { get; set; } = null!;
    public ICollection<InventoryMovement> InventoryMovements  { get; set; } = [];
}
