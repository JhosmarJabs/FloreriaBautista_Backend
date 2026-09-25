namespace FloreriaBautista.Models.DTOs.Inventory;

public class InventoryIndexDto
{
    public Guid     Id                  { get; set; }
    public string   Nombre              { get; set; } = string.Empty;
    public int      StockActual         { get; set; }
    public int      StockMinimo         { get; set; }
    public string   Sucursal            { get; set; } = string.Empty;
    public bool     SumaAlCosto         { get; set; }
    public string?  UnidadMedida        { get; set; }
    public decimal  PrecioCosto         { get; set; }
    public bool     EsFlorPrimaria      { get; set; }
    public string?  ImagenUrl           { get; set; }
    public bool     Activo              { get; set; }
    public decimal  RendimientoEsperado { get; set; }
    public decimal  FactorMermaUso      { get; set; }
    public decimal? PrecioUnidadCompra  { get; set; }
    public string?  UnidadCompra        { get; set; }
    public int?     VidaUtilDias        { get; set; }
    public DateTime ActualizadoEn       { get; set; }
}
