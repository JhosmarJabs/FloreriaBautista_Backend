namespace FloreriaBautista.Models.Entities;

public class Category
{
    public Guid    Id          { get; set; }
    public string  Nombre      { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string  Estado      { get; set; } = "ACTIVA";
    public bool    Activo      { get; set; } = true;

    public ICollection<ProductCategory> ProductCategories { get; set; } = [];
}
