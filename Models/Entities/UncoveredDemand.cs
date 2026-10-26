namespace FloreriaBautista.Models.Entities;

public class UncoveredDemand
{
    public Guid     Id                  { get; set; }
    public Guid     InventoryItemId     { get; set; }
    public Guid?    ProductId           { get; set; }
    public int      CantidadSolicitada  { get; set; }
    public int      CantidadDisponible  { get; set; }
    public int      Deficit             { get; set; }
    public string   Origen              { get; set; } = string.Empty; // VENTA_INSTANTANEA / VENTA_ANTICIPADA / VALIDACION_WEB
    public DateTime FechaRegistro       { get; set; } = DateTime.UtcNow;

    public InventoryItem InventoryItem  { get; set; } = null!;
    public Product?      Product        { get; set; }
}
