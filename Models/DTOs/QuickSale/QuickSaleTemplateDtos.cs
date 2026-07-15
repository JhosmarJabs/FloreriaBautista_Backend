namespace FloreriaBautista.Models.DTOs.QuickSale;

public class QuickSaleTemplateItemDto
{
    public Guid    Id        { get; set; }
    public Guid    ProductId { get; set; }
    public string  Nombre    { get; set; } = string.Empty;
    public decimal Precio    { get; set; }
    public string  Icono     { get; set; } = string.Empty;
    public string  Color     { get; set; } = string.Empty;
}

public class QuickSaleTemplateDto
{
    public Guid    Id          { get; set; }
    public string  Nombre      { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string  Icono       { get; set; } = string.Empty;
    public int     Orden       { get; set; }

    public List<QuickSaleTemplateItemDto> Items { get; set; } = [];
}

public class SaveQuickSaleTemplateItemRequestDto
{
    public Guid   ProductId { get; set; }
    public string Icono     { get; set; } = "Sparkles";
    public string Color     { get; set; } = "blue";
}

public class SaveQuickSaleTemplateRequestDto
{
    public string  Nombre      { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string  Icono       { get; set; } = "Sparkles";
    public int     Orden       { get; set; } = 0;

    public List<SaveQuickSaleTemplateItemRequestDto> Items { get; set; } = [];
}
