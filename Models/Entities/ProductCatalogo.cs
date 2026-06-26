using System.Text.Json.Serialization;

namespace FloreriaBautista.Models.Entities;

public class ProductCatalogo
{
    public Guid ProductId { get; set; }
    
    [JsonIgnore]
    public Product? Product { get; set; }

    public Guid CatalogoId { get; set; }
    
    [JsonIgnore]
    public Catalogo? Catalogo { get; set; }
}
