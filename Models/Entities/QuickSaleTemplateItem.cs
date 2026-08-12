namespace FloreriaBautista.Models.Entities;

public class QuickSaleTemplateItem
{
    public Guid   Id                  { get; set; }
    public Guid   QuickSaleTemplateId { get; set; }
    public Guid   ProductId           { get; set; }
    public string Icono               { get; set; } = "Sparkles";
    public string Color               { get; set; } = "blue";
    public int    Orden               { get; set; } = 0;
    // Cantidad que agrega el botón de un solo toque (ej: "10 Girasoles" -> 10).
    public int    CantidadPreset      { get; set; } = 1;

    public QuickSaleTemplate Template { get; set; } = null!;
    public Product           Product  { get; set; } = null!;
}
