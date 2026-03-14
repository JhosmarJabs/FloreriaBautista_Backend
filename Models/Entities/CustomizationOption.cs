namespace FloreriaBautista.Models.Entities;

public class CustomizationOption
{
    public Guid    Id       { get; set; }
    public string  Clave    { get; set; } = string.Empty;
    public string  Nombre   { get; set; } = string.Empty;
    public string  Tipo     { get; set; } = string.Empty; // TEXTO / LISTA / BOOLEANO
    public string? Opciones { get; set; }                 // JSON array si Tipo = LISTA

    public ICollection<ProductCustomizationOption>  ProductCustomizationOptions  { get; set; } = [];
    public ICollection<OrderItemCustomization>      OrderItemCustomizations      { get; set; } = [];
}
