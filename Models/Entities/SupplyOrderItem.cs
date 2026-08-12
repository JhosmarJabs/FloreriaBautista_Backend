namespace FloreriaBautista.Models.Entities;

/// <summary>
/// Una línea de la solicitud: qué insumo se pidió, cuánto, y qué llegó realmente.
/// </summary>
public class SupplyOrderItem
{
    public Guid      Id                  { get; set; }
    public Guid      SupplyOrderId       { get; set; }
    public Guid      InventoryItemId     { get; set; }
    public string    NombreSnapshot      { get; set; } = string.Empty; // nombre del insumo al solicitar (por si se renombra)
    public string?   UnidadMedida        { get; set; }
    public int       CantidadSolicitada  { get; set; }
    public int?      CantidadRecibida    { get; set; }               // null = aún no confirmado
    public string    EstadoLinea         { get; set; } = "PENDIENTE"; // PENDIENTE / COMPLETO / PARCIAL / NO_LLEGO / EXCEDENTE

    // Se congela con el PrecioCosto del insumo al crear la solicitud (es lo que alimenta
    // TotalEstimado) y se sobrescribe con el costo real si se captura en la recepción.
    public decimal?  PrecioUnitario      { get; set; }

    public string    Origen              { get; set; } = "Manual";   // "Modelo S1 · 2026-W33 · San Valentín" o "Manual"
    public string?   Observacion         { get; set; }               // "vino marchito", "mandaron rosa roja en vez de blanca"
    public DateTime? RecibidoEn          { get; set; }
    public Guid?     InventoryMovementId { get; set; }               // trazabilidad al movimiento que generó la confirmación

    public SupplyOrder        SupplyOrder       { get; set; } = null!;
    public InventoryItem      InventoryItem     { get; set; } = null!;
    public InventoryMovement? InventoryMovement { get; set; }
}
