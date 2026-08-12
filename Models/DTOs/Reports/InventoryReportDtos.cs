namespace FloreriaBautista.Models.DTOs.Reports;

// ─── Movimientos de insumos (kardex) ─────────────────────────────────────────
// Fuente: inventory_movements + inventory_items.PrecioCosto + users
//
// Convención de la tabla (ver InventoryService.RegistrarMovimientoAsync):
//   ENTRADA → stock += Cantidad
//   SALIDA  → stock -= Cantidad
//   AJUSTE  → stock  = Cantidad   (valor ABSOLUTO, no un delta)
// Por eso el kardex no puede sumar cantidades a ciegas: se reconstruye el saldo
// corrido aplicando la regla de cada tipo.

public class InventoryMovementsReportDto
{
    public int     TotalMovimientos { get; set; }
    public int     TotalEntradas    { get; set; }
    public int     TotalSalidas     { get; set; }
    public int     TotalAjustes     { get; set; }
    /// Valorización a InventoryItem.PrecioCosto vigente.
    public decimal ValorEntradas    { get; set; }
    public decimal ValorSalidas     { get; set; }

    public List<TimeBucketDto>       SerieSalidas  { get; set; } = [];
    public List<TimeBucketDto>       SerieEntradas { get; set; } = [];
    public List<SupplyConsumptionDto> TopConsumo   { get; set; } = [];
    public List<BreakdownDto>        PorUsuario    { get; set; } = [];

    /// Kardex detallado. Solo se llena cuando se filtra por un insumo concreto:
    /// el saldo corrido no tiene sentido mezclando insumos distintos.
    public KardexDto? Kardex { get; set; }
}

public class SupplyConsumptionDto
{
    public Guid    InventoryItemId { get; set; }
    public string  Nombre          { get; set; } = string.Empty;
    public string? UnidadMedida    { get; set; }
    public int     Consumido       { get; set; }
    public int     Recibido        { get; set; }
    public decimal PrecioCosto     { get; set; }
    public decimal ValorConsumido  { get; set; }
    public int     StockActual     { get; set; }
}

public class KardexDto
{
    public Guid    InventoryItemId  { get; set; }
    public string  Nombre           { get; set; } = string.Empty;
    public string? UnidadMedida     { get; set; }
    public decimal PrecioCosto      { get; set; }

    /// Saldo al iniciar el periodo. Null cuando no se puede reconstruir: pasa si
    /// el insumo tiene su primer AJUSTE (que fija el stock en absoluto) DENTRO
    /// del periodo y no hay ningún ancla anterior. Se prefiere decir "no se sabe"
    /// antes que inventar un número.
    public int?    SaldoInicial     { get; set; }
    public int     SaldoFinal       { get; set; }
    public List<KardexLineDto> Lineas { get; set; } = [];
}

public class KardexLineDto
{
    public Guid     MovimientoId { get; set; }
    public DateTime FechaHora    { get; set; }
    public string   Tipo         { get; set; } = string.Empty;
    public int      Cantidad     { get; set; }
    /// Efecto real sobre el stock. Para AJUSTE es (nuevo − anterior), que es lo
    /// que de verdad entró o salió; null si el saldo anterior es desconocido.
    public int?     Delta        { get; set; }
    public int?     Saldo        { get; set; }
    public string?  Motivo       { get; set; }
    public string   Usuario      { get; set; } = string.Empty;
}

// ─── Merma y caducidad ───────────────────────────────────────────────────────
// Fuente: inventory_movements tipo AJUSTE con delta negativo.

public class WasteReportDto
{
    public int     UnidadesPerdidas { get; set; }
    public decimal ValorPerdido     { get; set; }
    /// Valor de la merma sobre el valor total de las salidas del periodo, 0-100.
    public decimal? PorcentajeSobreConsumo { get; set; }

    public List<TimeBucketDto> Serie      { get; set; } = [];
    public List<WasteItemDto>  PorInsumo  { get; set; } = [];
    public List<BreakdownDto>  PorMotivo  { get; set; } = [];
}

public class WasteItemDto
{
    public Guid    InventoryItemId { get; set; }
    public string  Nombre          { get; set; } = string.Empty;
    public string? UnidadMedida    { get; set; }
    public int     Unidades        { get; set; }
    public decimal Valor           { get; set; }
    public int     Eventos         { get; set; }
}

// ─── Stock muerto / sin rotación ─────────────────────────────────────────────

public class DeadStockReportDto
{
    public int     DiasSinMovimiento    { get; set; }
    public decimal CapitalInmovilizado  { get; set; }
    public List<DeadStockItemDto>    Insumos   { get; set; } = [];
    public List<DeadStockProductDto> Productos { get; set; } = [];
}

public class DeadStockItemDto
{
    public Guid      InventoryItemId  { get; set; }
    public string    Nombre           { get; set; } = string.Empty;
    public string?   UnidadMedida     { get; set; }
    public int       StockActual      { get; set; }
    public decimal   PrecioCosto      { get; set; }
    public decimal   CapitalDetenido  { get; set; }
    public DateTime? UltimoMovimiento { get; set; }
    /// Null cuando nunca ha tenido un movimiento registrado.
    public int?      DiasSinMover     { get; set; }
}

public class DeadStockProductDto
{
    public Guid      ProductId     { get; set; }
    public string    Nombre        { get; set; } = string.Empty;
    public decimal   PrecioBase    { get; set; }
    public DateTime? UltimaVenta   { get; set; }
    public int?      DiasSinVender { get; set; }
}
