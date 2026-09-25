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

    // ── Rendimiento y merma ──────────────────────────────────────
    public decimal  RendimientoEsperado { get; set; }
    public decimal  FactorMermaUso      { get; set; }
    public decimal? PrecioUnidadCompra  { get; set; }
    public string?  UnidadCompra        { get; set; }
    public int?     VidaUtilDias        { get; set; }
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
    public bool                EsFlorPrimaria { get; set; }
    public string?            ImagenUrl    { get; set; }

    /// Si no se manda (o llega ≤0), se asume 1 = sin conversión unidad-de-compra → unidad-de-uso.
    public decimal             RendimientoEsperado { get; set; } = 1m;
    /// Fracción esperada de pérdida por manipulación (0–0.9). 0 = sin merma automática.
    public decimal             FactorMermaUso      { get; set; } = 0m;
    /// Si se manda, PrecioCosto se deriva de PrecioUnidadCompra / RendimientoEsperado
    /// salvo que también se mande PrecioCosto explícito en la misma petición.
    public decimal?            PrecioUnidadCompra  { get; set; }
    public string?             UnidadCompra        { get; set; }
    public int?                VidaUtilDias        { get; set; }
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

    public decimal? RendimientoEsperado { get; set; }
    public decimal? FactorMermaUso      { get; set; }
    public decimal? PrecioUnidadCompra  { get; set; }
    public string?  UnidadCompra        { get; set; }
    public int?     VidaUtilDias        { get; set; }
}

public class RegisterMovementRequestDto
{
    [Required] public Guid   InventoryItemId { get; set; }
    [Required] public string Tipo            { get; set; } = string.Empty; // ENTRADA / SALIDA / AJUSTE

    /// Para ENTRADA con UnidadesCompra, es informativo (se recalcula). Para el resto,
    /// es la cantidad real: unidades a mover (ENTRADA/SALIDA) o conteo absoluto (AJUSTE).
    public int                Cantidad        { get; set; }

    public string?           Motivo          { get; set; }

    /// RECEPCION / MANIPULACION / CADUCIDAD / CONTEO_FISICO / OTRO. Si se omite, el
    /// servicio asigna una por defecto según el tipo (ver InventoryService).
    public string?           MotivoCategoria { get; set; }

    /// Solo para ENTRADA: unidades de compra recibidas (ej. 3 rollos, 2 cajas). Si se
    /// manda, la cantidad efectiva se calcula con el rendimiento esperado del insumo
    /// (o con RendimientoObservado, si se manda) en vez de exigir el conteo exacto.
    public int?               UnidadesCompra  { get; set; }

    /// Solo para ENTRADA con UnidadesCompra: rendimiento real de esta recepción en
    /// particular (ej. "este rollo trajo 9 varitas"), si el empleado lo contó. Si se
    /// manda, además recalibra RendimientoEsperado del insumo para futuras recepciones.
    public decimal?           RendimientoObservado { get; set; }
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
    public string   MotivoCategoria { get; set; } = "OTRO";
    /// Unidades que el sistema descontó automáticamente como merma de manipulación
    /// estimada, además de lo solicitado. Null si no aplicó (SALIDA sin FactorMermaUso,
    /// o movimiento que no es una salida de consumo).
    public int?      MermaAutomaticaGenerada { get; set; }
    public DateTime FechaHora       { get; set; }
}
