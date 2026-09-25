namespace FloreriaBautista.Models.Entities;

public class InventoryItem
{
    public Guid    Id            { get; set; }
    public string  Nombre        { get; set; } = string.Empty;
    public int     StockActual   { get; set; } = 0;
    public int     StockMinimo   { get; set; } = 0;
    public string  Sucursal      { get; set; } = string.Empty;
    public decimal PrecioCosto   { get; set; } = 0;
    public bool    EsFlorPrimaria { get; set; } = false;
    public bool    SumaAlCosto   { get; set; } = true;
    public string? UnidadMedida  { get; set; }
    public string? ImagenUrl     { get; set; }
    public bool     Activo        { get; set; } = true;
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;

    // ── Rendimiento y merma ──────────────────────────────────────
    // Un rollo de solidago no siempre trae el mismo número de varitas, y una caja de
    // gerberas rara vez rinde el 100% de lo que dice la etiqueta. En vez de exigir un
    // conteo exacto en cada recepción, el sistema mantiene una estimación de cuánto
    // rinde realmente cada insumo y cuánto se pierde al manipularlo, y la recalibra
    // sola con cada recepción y cada conteo físico (ver InventoryService.RegistrarMovimientoAsync).

    /// <summary>Unidades de uso (varas, flores, gramos) que rinde en promedio 1 unidad
    /// de compra (rollo, caja, paquete). 1 = sin conversión: se compra y se usa igual.</summary>
    public decimal RendimientoEsperado { get; set; } = 1m;

    /// <summary>Fracción (0–0.9) que se espera perder al manipular el insumo (se rompe,
    /// se maltrata al armar). Se aplica automáticamente al registrar una SALIDA normal;
    /// el empleado no registra la pérdida por separado.</summary>
    public decimal FactorMermaUso { get; set; } = 0m;

    /// <summary>Precio pagado por 1 unidad de compra (rollo, caja). Si está definido,
    /// PrecioCosto (costo por unidad de uso) se deriva de PrecioUnidadCompra / RendimientoEsperado
    /// en vez de capturarse a mano.</summary>
    public decimal? PrecioUnidadCompra { get; set; }

    /// <summary>Unidad en la que se compra el insumo (ROLLO, CAJA, PAQUETE...), distinta
    /// de UnidadMedida, que es la unidad en la que se usa en receta (VARA, FLOR, GRAMO...).</summary>
    public string?  UnidadCompra  { get; set; }

    /// <summary>Vida útil esperada en días para insumos perecederos. Null = no perecedero
    /// o sin seguimiento de vida útil.</summary>
    public int?     VidaUtilDias  { get; set; }

    public ICollection<InventoryMovement> InventoryMovements { get; set; } = [];
    public ICollection<ProductRecipe>     ProductRecipes     { get; set; } = [];
}
