namespace FloreriaBautista.Models.Entities;

public class ProductCustomizationOption
{
    public Guid ProductId              { get; set; }
    public Guid CustomizationOptionId  { get; set; }

    public Product             Product             { get; set; } = null!;
    public CustomizationOption CustomizationOption { get; set; } = null!;
}
