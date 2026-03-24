using System.ComponentModel.DataAnnotations;

namespace FloreriaBautista.Models.DTOs.Flowers;

public class FlowerDto
{
    public Guid    Id           { get; set; }
    public string  Nombre       { get; set; } = string.Empty;
    public string  Color        { get; set; } = string.Empty;
    public decimal PrecioCosto  { get; set; }
    public string  UnidadMedida  { get; set; } = string.Empty;
    public bool    EsFlorPrimaria { get; set; }
    public int     StockActual   { get; set; }
    public int     StockMinimo  { get; set; }
    public string  Estado       { get; set; } = string.Empty;
    public bool    BajoMinimo   => StockActual <= StockMinimo;
    public DateTime CreadoEn   { get; set; }
}

public class CreateFlowerRequestDto
{
    [Required] public string  Nombre       { get; set; } = string.Empty;
    [Required] public string  Color        { get; set; } = string.Empty;
    [Range(0, double.MaxValue)]
    public decimal             PrecioCosto  { get; set; }
    public string              UnidadMedida  { get; set; } = "TALLO";
    public bool                EsFlorPrimaria { get; set; } = false;
    public int                 StockMinimo   { get; set; }
}

public class UpdateFlowerRequestDto
{
    public string?  Nombre       { get; set; }
    public string?  Color        { get; set; }
    public decimal? PrecioCosto  { get; set; }
    public string?  UnidadMedida  { get; set; }
    public bool?    EsFlorPrimaria { get; set; }
    public int?     StockMinimo  { get; set; }
    public string?  Estado       { get; set; }
}

public class FlowerMovementDto
{
    public Guid     Id        { get; set; }
    public Guid     FlowerId  { get; set; }
    public string   Flor      { get; set; } = string.Empty;
    public string   Tipo      { get; set; } = string.Empty;
    public int      Cantidad  { get; set; }
    public int      StockAntes   { get; set; }
    public int      StockDespues { get; set; }
    public string?  Motivo    { get; set; }
    public DateTime FechaHora { get; set; }
}

public class RegisterFlowerMovementDto
{
    [Required] public Guid   FlowerId { get; set; }
    [Required] public string Tipo     { get; set; } = string.Empty;
    [Range(1, int.MaxValue)]
    public int               Cantidad { get; set; }
    public string?           Motivo   { get; set; }
}

public class ProductFlowerDto
{
    public Guid   FlowerId    { get; set; }
    public string NombreFlor  { get; set; } = string.Empty;
    public string Color       { get; set; } = string.Empty;
    public int    Cantidad    { get; set; }
    public string UnidadMedida { get; set; } = string.Empty;
}

public class UpdateProductFlowersDto
{
    public List<ProductFlowerItemDto> Flores { get; set; } = [];
}

public class ProductFlowerItemDto
{
    [Required] public Guid FlowerId { get; set; }
    [Range(1, int.MaxValue)]
    public int             Cantidad { get; set; } = 1;
}

public class CostoProductoDto
{
    public Guid    ProductId        { get; set; }
    public string  NombreProducto   { get; set; } = string.Empty;
    public decimal CostoBase        { get; set; }
    public decimal MargenFactor     { get; set; }
    public decimal PrecioSugerido   { get; set; }
    public List<InsumoCalculoDto> Insumos { get; set; } = [];
}

public class InsumoCalculoDto
{
    public Guid    FlowerId       { get; set; }
    public string  Nombre         { get; set; } = string.Empty;
    public string  Color          { get; set; } = string.Empty;
    public bool    EsFlorPrimaria { get; set; }
    public int     Cantidad       { get; set; }
    public decimal CostoUnitario  { get; set; }
    public decimal Subtotal       { get; set; }
}

public class ActualizarPrecioRequestDto
{
    public decimal  MargenFactor  { get; set; } = 2.5m;
    public decimal? PrecioFinal   { get; set; } // Si null, usa el sugerido
}
