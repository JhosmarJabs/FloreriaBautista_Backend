namespace FloreriaBautista.Models.DTOs.Inventory;

public class SupplyAvailabilityDto
{
    public Guid   InventoryItemId         { get; set; }
    public string Nombre                  { get; set; } = string.Empty;
    public int    StockActual             { get; set; }
    public int    ComprometidoPendientes  { get; set; }
    public int    ComprometidoReservas    { get; set; }
    public int    Disponible              { get; set; }
}

public class ProductAvailabilityDto
{
    public Guid   ProductId               { get; set; }
    public string Nombre                  { get; set; } = string.Empty;
    public int    CantidadDisponible      { get; set; }
    public Guid?  InsumoCuelloDeBotella   { get; set; }
    public string? NombreCuelloDeBotella  { get; set; }
    public bool   TieneReceta             { get; set; }
    public List<InsumoDisponibilidadDto> DetallesPorInsumo { get; set; } = [];
}

public class InsumoDisponibilidadDto
{
    public Guid   InventoryItemId    { get; set; }
    public string Nombre             { get; set; } = string.Empty;
    public int    CantidadRequerida  { get; set; }
    public int    StockActual        { get; set; }
    public int    Comprometido       { get; set; }
    public int    Disponible         { get; set; }
    public int    AlcanzaPara        { get; set; } // Disponible / CantidadRequerida
}

public class SaleValidationResult
{
    public bool   PuedeVenderse            { get; set; }
    public int    CantidadMaximaDisponible { get; set; }
    public bool   DentroDeTolerancia       { get; set; }
    public int    Deficit                  { get; set; }
    public List<InsumoInsuficienteDto> InsumosInsuficientes { get; set; } = [];
}

public class InsumoInsuficienteDto
{
    public Guid   InventoryItemId   { get; set; }
    public string Nombre            { get; set; } = string.Empty;
    public int    Necesario         { get; set; }
    public int    Disponible        { get; set; }
    public int    Deficit           { get; set; }
}

public class AdvanceSaleValidationResult
{
    public bool     PuedeAceptarse          { get; set; }
    public bool     RequiereAbastecimiento  { get; set; }
    public DateTime? FechaLimiteCorte       { get; set; }
    public string?  Mensaje                 { get; set; }
    public DateOnly? FechaViableSugerida    { get; set; }
    public List<InsumoAbastecimientoDto> InsumosPorAbastecer { get; set; } = [];
}

public class InsumoAbastecimientoDto
{
    public Guid    InventoryItemId    { get; set; }
    public string  Nombre             { get; set; } = string.Empty;
    public int     CantidadFaltante   { get; set; }
    public DateOnly? FechaViable      { get; set; }
    public string?  Estado            { get; set; } // DISPONIBLE / REQUIERE_PEDIDO / SIN_TIEMPO
}

public class SupplyCycleDto
{
    public DateOnly ProximoAbastecimiento { get; set; }
    public DateTime Cutoff                { get; set; }
    public string   EstadoActual          { get; set; } = string.Empty; // ABIERTO / CERRADO
}

public class SupplySettingsDto
{
    public int      DiaAbastecimiento         { get; set; }
    public string   HoraCutoff                { get; set; } = string.Empty;
    public int      DiaCutoff                 { get; set; }
    public int      ToleranciaUnidades        { get; set; }
    public decimal  ToleranciaPorcentaje      { get; set; }
    public bool     UsarToleranciaAbsoluta    { get; set; }
    public decimal  MargenSeguridadPorcentaje { get; set; }
    public int      DiasMinimosAnticipacion   { get; set; }
    public bool     PermitirVentaSinReceta    { get; set; }
}
