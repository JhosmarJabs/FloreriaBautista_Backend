namespace FloreriaBautista.Models.DTOs.InstantSales;

public class InstantSaleResponseDto
{
    public Guid     Id              { get; set; }
    public Guid     ProductId       { get; set; }
    public string   ProductoNombre  { get; set; } = string.Empty;
    public int      Cantidad        { get; set; }
    public string   Estado          { get; set; } = string.Empty;
    public string?  MotivoRechazo   { get; set; }
    public DateTime CreadaEn        { get; set; }
    public DateTime? ReservaExpiraEn { get; set; }
}
