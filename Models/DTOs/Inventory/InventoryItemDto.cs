using System.ComponentModel.DataAnnotations;

namespace FloreriaBautista.Models.DTOs.Inventory;

public class InventoryItemDto
{
    public Guid    Id           { get; set; }
    public string  Nombre       { get; set; } = string.Empty;
    public int     StockActual  { get; set; }
    public int     StockMinimo  { get; set; }
    public string  Sucursal     { get; set; } = string.Empty;
    public bool    SumaAlCosto  { get; set; }
    public string? UnidadMedida { get; set; }
    public decimal PrecioCosto  { get; set; }
    public bool    EsFlorPrimaria { get; set; }
    public string? ImagenUrl    { get; set; }
    public bool    Activo       { get; set; }
    public bool    BajoMinimo   => StockActual <= StockMinimo;
    public int     UnidadesConsumidas { get; set; }
}

public class CreateInventoryItemDto
{
    [Required] public string  Nombre       { get; set; } = string.Empty;
    public int                StockActual  { get; set; } = 0;
    public int                StockMinimo  { get; set; } = 0;
    [Required] public string  Sucursal     { get; set; } = string.Empty;
    public bool               SumaAlCosto  { get; set; } = true;
    public string?            UnidadMedida { get; set; }
    public decimal            PrecioCosto  { get; set; }
    public bool               EsFlorPrimaria { get; set; }
    public string?            ImagenUrl    { get; set; }
}

public class UpdateInventoryItemDto
{
    public string?  Nombre       { get; set; }
    public int?     StockActual  { get; set; }
    public int?     StockMinimo  { get; set; }
    public string?  Sucursal     { get; set; }
    public bool?    SumaAlCosto  { get; set; }
    public string?  UnidadMedida { get; set; }
    public decimal? PrecioCosto  { get; set; }
    public bool?    EsFlorPrimaria { get; set; }
    public string?  ImagenUrl    { get; set; }
    public bool?    Activo       { get; set; }
}

public class RegisterMovementRequestDto
{
    [Required] public Guid   InventoryItemId { get; set; }
    [Required] public string Tipo            { get; set; } = string.Empty; // ENTRADA / SALIDA / AJUSTE
    [Required] [Range(1, int.MaxValue)]
    public int               Cantidad        { get; set; }
    public string?           Motivo          { get; set; }
}

public class InventoryMovementDto
{
    public Guid     Id              { get; set; }
    public Guid     InventoryItemId { get; set; }
    public string   NombreItem      { get; set; } = string.Empty;
    public string   Tipo            { get; set; } = string.Empty;
    public int      Cantidad        { get; set; }
    public int      StockAntes      { get; set; }
    public int      StockDespues    { get; set; }
    public string?  Motivo          { get; set; }
    public DateTime FechaHora       { get; set; }
}
