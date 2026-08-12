namespace FloreriaBautista.Models.DTOs.Reports;

// ─── Reporte de ventas ───────────────────────────────────────────────────────
// Fuente: orders + payments + order_items.
//
// Se mide por Order.FechaCreacion (fecha real de la venta), NO por FechaEntrega:
// un pedido anticipado capturado hoy para entregar en tres semanas es dinero que
// entró hoy. FechaEntrega se usa solo en los reportes de entrega.

public class SalesOverviewDto
{
    public int      TotalPedidos     { get; set; }
    public decimal  TotalVentas      { get; set; }
    public decimal  TicketPromedio   { get; set; }
    /// Suma de pagos efectivamente registrados en el periodo (puede diferir de
    /// TotalVentas: hay pedidos anticipados que se cobran en otro periodo).
    public decimal  TotalCobrado     { get; set; }
    public decimal  SaldoPorCobrar   { get; set; }
    public int      PedidosCancelados{ get; set; }

    public ComparisonDto VentasVsAnterior  { get; set; } = new();
    public ComparisonDto PedidosVsAnterior { get; set; } = new();
    public ComparisonDto TicketVsAnterior  { get; set; } = new();

    public string             Granularidad  { get; set; } = "dia";
    public List<TimeBucketDto> Serie        { get; set; } = [];
    public List<BreakdownDto>  PorCanal     { get; set; } = [];
    public List<BreakdownDto>  PorTipo      { get; set; } = [];
    public List<BreakdownDto>  PorMetodoPago{ get; set; } = [];
}

// ─── Rentabilidad y margen por producto ──────────────────────────────────────
// Fuente: order_items + product_recipes + inventory_items.PrecioCosto

public class ProfitabilityReportDto
{
    public decimal  IngresosTotales   { get; set; }
    /// Costo de los productos que SÍ tienen receta cargada.
    public decimal  CostoTotal        { get; set; }
    public decimal  MargenTotal       { get; set; }
    public decimal? MargenPct         { get; set; }

    /// Cuántos productos vendidos no tienen receta: su costo es desconocido y
    /// queda FUERA de CostoTotal/MargenTotal. Tratarlo como costo cero inflaría
    /// el margen, así que se reporta aparte.
    public int      ProductosSinReceta { get; set; }
    public decimal  IngresosSinCostear { get; set; }

    public List<ProductProfitDto> Productos { get; set; } = [];
}

public class ProductProfitDto
{
    public Guid     ProductId      { get; set; }
    public string   Nombre         { get; set; } = string.Empty;
    public int      Unidades       { get; set; }
    public decimal  Ingresos       { get; set; }
    public decimal  PrecioPromedio { get; set; }
    public bool     TieneReceta    { get; set; }
    public decimal? CostoUnitario  { get; set; }
    public decimal? CostoTotal     { get; set; }
    public decimal? Margen         { get; set; }
    public decimal? MargenPct      { get; set; }
}

// ─── Estacionalidad por festividad ───────────────────────────────────────────
// Fuente: catalogos.MesDiaInicio/MesDiaFin + product_catalogos + order_items
//         + inventory_movements (consumo de insumos en la ventana)

public class SeasonalityReportDto
{
    public List<FestivityDto> Festividades { get; set; } = [];
}

public class FestivityDto
{
    public Guid    CatalogoId   { get; set; }
    public string  Nombre       { get; set; } = string.Empty;
    public string? MesDiaInicio { get; set; }
    public string? MesDiaFin    { get; set; }
    public List<FestivityYearDto> Anios { get; set; } = [];
}

public class FestivityYearDto
{
    public int      Anio                { get; set; }
    public DateOnly Inicio              { get; set; }
    public DateOnly Fin                 { get; set; }
    /// Ventas totales de la florería durante la ventana (el "efecto temporada").
    public int      Pedidos             { get; set; }
    public decimal  VentasTotales       { get; set; }
    /// Ventas solo de los productos que pertenecen a ese catálogo.
    public int      UnidadesDelCatalogo { get; set; }
    public decimal  VentasDelCatalogo   { get; set; }
    /// Insumos consumidos (movimientos SALIDA) en la ventana. Es lo que dice
    /// cuánto comprar la próxima temporada.
    public int      ConsumoInsumos      { get; set; }
}
