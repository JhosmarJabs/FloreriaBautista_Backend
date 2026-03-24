namespace FloreriaBautista.Models.Entities;

public class ProductFlower
{
    public Guid ProductId { get; set; }
    public Guid FlowerId  { get; set; }
    public int      Cantidad              { get; set; } = 1;
    public decimal  CostoUnitarioSnapshot { get; set; } = 0;

    public Product Product { get; set; } = null!;
    public Flower  Flower  { get; set; } = null!;
}
