namespace FloreriaBautista.Models.DTOs.Inventory;

public class InventoryItemDto
{
    public Guid    Id           { get; set; }
    public Guid    ProductId    { get; set; }
    public string  Nombre       { get; set; } = string.Empty;
    public int     StockActual  { get; set; }
    public int     StockMinimo  { get; set; }
    public string  Sucursal     { get; set; } = string.Empty;
    public bool    BajoMinimo   => StockActual <= StockMinimo;
}

public class RegisterMovementRequestDto
{
    public Guid    ProductId   { get; set; }
    public string  Tipo        { get; set; } = string.Empty; // ENTRADA / SALIDA / AJUSTE
    public int     Cantidad    { get; set; }
    public string? Motivo      { get; set; }
}

public class InventoryMovementDto
{
    public Guid     Id          { get; set; }
    public Guid     ProductId   { get; set; }
    public string   Producto    { get; set; } = string.Empty;
    public string   Tipo        { get; set; } = string.Empty;
    public int      Cantidad    { get; set; }
    public int      StockAntes  { get; set; }
    public int      StockDespues { get; set; }
    public string?  Motivo      { get; set; }
    public DateTime FechaHora   { get; set; }
}
