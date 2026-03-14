namespace FloreriaBautista.Models.Entities;

public class Address
{
    public Guid    Id          { get; set; }
    public Guid    CustomerId  { get; set; }
    public string? Etiqueta    { get; set; }
    public string  Calle       { get; set; } = string.Empty;
    public string  Colonia     { get; set; } = string.Empty;
    public string  Municipio   { get; set; } = string.Empty;
    public string  Estado      { get; set; } = string.Empty;
    public string? Cp          { get; set; }
    public string? Referencias { get; set; }
    public DateTime CreadoEn  { get; set; } = DateTime.UtcNow;

    public Customer Customer { get; set; } = null!;
}
