namespace FloreriaBautista.Models.Entities;

public class ProductCollection
{
    public Guid ProductId    { get; set; }
    public Guid CollectionId { get; set; }

    public Product    Product    { get; set; } = null!;
    public Collection Collection { get; set; } = null!;
}
