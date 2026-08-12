namespace FloreriaBautista.Models.DTOs.Reports;

// ─── Reporte de compras ──────────────────────────────────────────────────────
// Fuente: supply_orders + supply_order_items (módulo de reabastecimiento).
//
// Si las tablas todavía no existen en la base (la migración del módulo aún no se
// aplicó), el servicio devuelve el reporte marcado como no disponible en vez de
// devolver ceros: un cero aquí se leería como "no compramos nada".

public class PurchasingReportDto
{
    public int      TotalSolicitudes   { get; set; }
    public decimal  GastoEstimado      { get; set; }
    /// Σ CantidadRecibida × PrecioUnitario de las líneas ya confirmadas.
    public decimal  GastoRecibido      { get; set; }
    public int      LineasTotales      { get; set; }
    public int      LineasCompletas    { get; set; }
    public decimal? PctLineasCompletas { get; set; }
    /// Promedio de horas entre FechaEnvio y FechaRecepcion.
    public double?  HorasEnvioRecepcion{ get; set; }

    public List<TimeBucketDto>        Serie             { get; set; } = [];
    public List<SupplierPerformanceDto> Proveedores     { get; set; } = [];
    public List<BreakdownDto>         PorEstado         { get; set; } = [];
    public List<SupplyFulfillmentDto> InsumosProblema   { get; set; } = [];
    public List<SupplyCostTrendDto>   EvolucionCostos   { get; set; } = [];
}

public class SupplierPerformanceDto
{
    public string   Proveedor          { get; set; } = string.Empty;
    public int      Solicitudes        { get; set; }
    public decimal  GastoEstimado      { get; set; }
    public decimal  GastoRecibido      { get; set; }
    public int      LineasTotales      { get; set; }
    public int      LineasCompletas    { get; set; }
    public decimal? PctLineasCompletas { get; set; }
    public int      UnidadesSolicitadas{ get; set; }
    public int      UnidadesRecibidas  { get; set; }
    public decimal? PctSurtido         { get; set; }
    public double?  HorasEnvioRecepcion{ get; set; }
}

/// Insumos que el proveedor más falla en surtir.
public class SupplyFulfillmentDto
{
    public Guid     InventoryItemId    { get; set; }
    public string   Nombre             { get; set; } = string.Empty;
    public int      Lineas             { get; set; }
    public int      LineasIncompletas  { get; set; }
    public int      UnidadesSolicitadas{ get; set; }
    public int      UnidadesRecibidas  { get; set; }
    public int      Faltante           { get; set; }
    public decimal? PctSurtido         { get; set; }
}

/// Evolución del costo unitario pagado por un insumo a lo largo del periodo.
public class SupplyCostTrendDto
{
    public Guid    InventoryItemId { get; set; }
    public string  Nombre          { get; set; } = string.Empty;
    public decimal CostoPrimero    { get; set; }
    public decimal CostoUltimo     { get; set; }
    public decimal CostoPromedio   { get; set; }
    public decimal? VariacionPct   { get; set; }
    public List<TimeBucketDto> Serie { get; set; } = [];
}
