using System.Text.Json.Serialization;

namespace FloreriaBautista.Models.DTOs.Analytics;

// Variables de entrada del modelo de la Solución 1.
// Los nombres JSON replican EXACTAMENTE los del dataset con el que se entrenó
// (05_Datasets/dataset_surtido.csv). El orden canónico vive en
// 07_Modelos/metadata_surtido.json -> orden_columnas_entrada.
public class SupplyFeaturesDto
{
    [JsonPropertyName("mes_objetivo")]                     public int     MesObjetivo { get; set; }
    [JsonPropertyName("es_temporada_alta")]                public int     EsTemporadaAlta { get; set; }
    [JsonPropertyName("cant_semana_actual")]               public decimal CantSemanaActual { get; set; }
    [JsonPropertyName("cant_semana_anterior")]             public decimal CantSemanaAnterior { get; set; }
    [JsonPropertyName("cant_2_semanas_atras")]             public decimal Cant2SemanasAtras { get; set; }
    [JsonPropertyName("promedio_movil_4")]                 public decimal PromedioMovil4 { get; set; }
    [JsonPropertyName("promedio_movil_8")]                 public decimal PromedioMovil8 { get; set; }
    [JsonPropertyName("cant_misma_semana_anio_anterior")]  public decimal CantMismaSemanaAnioAnterior { get; set; }
    [JsonPropertyName("num_pedidos_semana_actual")]        public decimal NumPedidosSemanaActual { get; set; }
    [JsonPropertyName("es_flor_primaria")]                 public int     EsFlorPrimaria { get; set; }
    [JsonPropertyName("variacion_pct_semana_anterior")]    public decimal VariacionPctSemanaAnterior { get; set; }
    [JsonPropertyName("temporada_objetivo")]               public string  TemporadaObjetivo { get; set; } = "SIN_TEMPORADA";
    [JsonPropertyName("unidad_medida")]                    public string  UnidadMedida { get; set; } = "TALLO";
}

public class SupplyPredictionResultDto
{
    [JsonPropertyName("consumo_predicho")] public decimal ConsumoPredicho { get; set; }
    [JsonPropertyName("algoritmo")]        public string  Algoritmo { get; set; } = string.Empty;
    [JsonPropertyName("version_sklearn")]  public string  VersionSklearn { get; set; } = string.Empty;
}

public class MlServiceHealthDto
{
    [JsonPropertyName("estado")]             public string  Estado { get; set; } = "desconocido";
    [JsonPropertyName("modelo_cargado")]     public bool    ModeloCargado { get; set; }
    [JsonPropertyName("error_carga")]        public string? ErrorCarga { get; set; }
    [JsonPropertyName("version_sklearn")]    public string? VersionSklearn { get; set; }
    [JsonPropertyName("algoritmo")]          public string? Algoritmo { get; set; }
    [JsonPropertyName("modelo_generado_en")] public string? ModeloGeneradoEn { get; set; }
}
