namespace FloreriaBautista.Models.DTOs.Inventory;

public class SupplyForecastDto
{
    public Guid   InventoryItemId { get; set; }
    public string Nombre          { get; set; } = string.Empty;
    public string? UnidadMedida   { get; set; }

    // Serie histórica semanal (consumo real, tipo_movimiento = SALIDA)
    public List<WeeklySupplyPointDto> Historico { get; set; } = [];

    // Semana objetivo de la predicción (ISO, ej. "2026-W17")
    public string SemanaObjetivo      { get; set; } = string.Empty;
    public string? TemporadaObjetivo  { get; set; } // festividad detectada en la semana a predecir, si aplica

    public int    ConsumoPredicho     { get; set; } // consumo estimado por el modelo para la semana objetivo
    public int    CantidadSugerida    { get; set; }
    public int    StockActual         { get; set; }
    public decimal CoberturaStockPct  { get; set; } // % de la sugerencia que ya cubre el stock actual

    public string MetodoCalculo { get; set; } = string.Empty; // explicación breve del método usado
}

// Un renglón de la lista de reabastecimiento: un insumo con la predicción del modelo S1.
// Es la vista operativa de la Solución 1: cuánto pedirle al proveedor de cada insumo.
public class SupplyReplenishmentItemDto
{
    public Guid    InventoryItemId   { get; set; }
    public string  Nombre            { get; set; } = string.Empty;
    public string? UnidadMedida      { get; set; }
    public int     StockActual       { get; set; }
    public int     StockMinimo       { get; set; }
    public int     ConsumoPredicho   { get; set; } // predicción del modelo para la semana objetivo
    public int     CantidadSugerida  { get; set; } // max(0, consumo predicho − stock actual)
    public string  SemanaObjetivo    { get; set; } = string.Empty;
    public string? TemporadaObjetivo { get; set; }
    public bool    BajoMinimo        { get; set; }
}

public class WeeklySupplyPointDto
{
    public string Semana           { get; set; } = string.Empty; // ISO, ej. "2026-W15"
    public DateOnly InicioSemana   { get; set; }
    public int CantidadConsumida   { get; set; }
    public string? Temporada       { get; set; } // nombre del catálogo/festividad si la semana cae en su ventana
}
