namespace FloreriaBautista.Models.Entities;

/// <summary>
/// Solicitud de reabastecimiento: el documento que se le manda al proveedor.
/// Nace de la lista armada con la predicción del modelo S1 y se cierra cuando
/// el proveedor surte y se confirma la recepción línea por línea.
/// </summary>
public class SupplyOrder
{
    public Guid      Id             { get; set; }
    public string    Folio          { get; set; } = string.Empty; // legible: "REAB-2026-0007"
    public string    Estado         { get; set; } = "BORRADOR";   // BORRADOR / ENVIADA / RECIBIDA_PARCIAL / RECIBIDA / CANCELADA
    public string?   Proveedor      { get; set; }                 // texto libre, todavía no hay catálogo de proveedores
    public DateTime  FechaSolicitud { get; set; } = DateTime.UtcNow;
    public DateTime? FechaEnvio     { get; set; }
    public DateTime? FechaRecepcion { get; set; }
    public string?   SemanaObjetivo { get; set; }                 // heredada de la predicción del modelo S1, ej. "2026-W33"
    public string?   Notas          { get; set; }
    public Guid      UsuarioId      { get; set; }
    public decimal   TotalEstimado  { get; set; }                 // Σ CantidadSolicitada × PrecioCosto al momento de crear

    public User Usuario { get; set; } = null!;
    public ICollection<SupplyOrderItem> Items { get; set; } = [];
}
