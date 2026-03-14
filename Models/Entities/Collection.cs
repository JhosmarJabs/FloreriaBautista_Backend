namespace FloreriaBautista.Models.Entities;

public class Collection
{
    public Guid    Id          { get; set; }
    public string  Nombre      { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string  Estado      { get; set; } = "ACTIVA";

    public ICollection<ProductCollection> ProductCollections { get; set; } = [];
}
