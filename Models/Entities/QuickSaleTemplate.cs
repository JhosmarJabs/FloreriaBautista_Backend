namespace FloreriaBautista.Models.Entities;

public class QuickSaleTemplate
{
    public Guid     Id            { get; set; }
    public string   Nombre        { get; set; } = string.Empty;
    public string?  Descripcion   { get; set; }
    public string   Icono         { get; set; } = "Sparkles";
    public int      Orden         { get; set; } = 0;
    public DateTime CreadoEn      { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;

    public ICollection<QuickSaleTemplateItem> Items { get; set; } = [];
}
