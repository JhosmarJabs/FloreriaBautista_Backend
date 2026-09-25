namespace FloreriaBautista.Models.Entities;

public class InventoryMovement
{
    public Guid     Id                { get; set; }
    public Guid     InventoryItemId   { get; set; }
    public string   TipoMovimiento    { get; set; } = string.Empty; // ENTRADA / SALIDA / AJUSTE
    public int      Cantidad          { get; set; }
    public string?  Motivo            { get; set; }
    // RECEPCION / MANIPULACION / CADUCIDAD / CONTEO_FISICO / OTRO. Reemplaza la
    // inferencia por texto libre del reporte de merma: cada movimiento ya declara
    // de dónde viene, en vez de que el reporte tenga que adivinarlo después.
    public string   MotivoCategoria   { get; set; } = "OTRO";
    public Guid     UsuarioId         { get; set; }
    public DateTime FechaHora         { get; set; } = DateTime.UtcNow;

    public InventoryItem InventoryItem { get; set; } = null!;
    public User          Usuario       { get; set; } = null!;
}
