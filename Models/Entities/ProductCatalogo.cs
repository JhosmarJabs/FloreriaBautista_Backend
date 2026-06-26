namespace FloreriaBautista.Models.Entities;

public class ProductCatalogo
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public Guid CatalogoId { get; set; }
    public Catalogo? Catalogo { get; set; }
}
