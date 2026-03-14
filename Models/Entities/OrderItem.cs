namespace FloreriaBautista.Models.Entities;

public class OrderItem
{
    public Guid    Id             { get; set; }
    public Guid    OrderId        { get; set; }
    public Guid    ProductId      { get; set; }
    public int     Cantidad       { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal       { get; set; }

    public Order   Order   { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public ICollection<OrderItemCustomization> OrderItemCustomizations { get; set; } = [];
}
