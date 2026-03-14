namespace FloreriaBautista.Models.Entities;

public class OrderItemCustomization
{
    public Guid    Id                     { get; set; }
    public Guid    OrderItemId            { get; set; }
    public Guid    CustomizationOptionId  { get; set; }
    public string? Valor                  { get; set; }

    public OrderItem           OrderItem           { get; set; } = null!;
    public CustomizationOption CustomizationOption { get; set; } = null!;
}
