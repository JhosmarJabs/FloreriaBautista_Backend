namespace FloreriaBautista.Models.DTOs.Inventory;

public class ForecastResultDto
{
    public Guid   InventoryItemId         { get; set; }
    public string Nombre                  { get; set; } = string.Empty;
    public string? UnidadMedida           { get; set; }
    public string SemanaObjetivo          { get; set; } = string.Empty;
    public int    ConsumoPredicho         { get; set; }
    public int    CantidadRecomendada     { get; set; }
    public int    RangoInferior           { get; set; }
    public int    RangoSuperior           { get; set; }
    public string NivelConfianza          { get; set; } = "SIN_DATOS";
    public int    StockActual             { get; set; }
    public int    DemandaFuturaConfirmada { get; set; }
    public ForecastFactorsDto Factores    { get; set; } = new();
    public string MetodoCalculo           { get; set; } = "Modelo matemático v1 — media ponderada con ajustes";
    public string Explicacion             { get; set; } = string.Empty;

    public int?    PrediccionML           { get; set; }
    public decimal? DiferenciaMLvsMathPct { get; set; }
}

public class ForecastFactorsDto
{
    public decimal ConsumoPromedioReciente   { get; set; }
    public decimal Tendencia                 { get; set; }
    public decimal DemandaNoCubierta         { get; set; }
    public decimal FactorTemporada           { get; set; } = 1.0m;
    public string? TemporadaDetectada        { get; set; }
    public decimal MermaEsperada             { get; set; }
    public decimal MargenSeguridad           { get; set; }
    public int     PedidosAnticipadosSemana  { get; set; }
}
