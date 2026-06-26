using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FloreriaBautista.Models.Entities;

public class InventoryDailySnapshot
{
    public Guid Id { get; set; }
    public Guid InventoryItemId { get; set; }
    public DateOnly Fecha { get; set; }
    public int StockFinal { get; set; }
    public int CantidadVendida { get; set; } // Suma de movimientos de SALIDA ese día
    public int CantidadRecibida { get; set; } // Suma de movimientos de ENTRADA ese día

    [ForeignKey("InventoryItemId")]
    public InventoryItem InventoryItem { get; set; } = null!;
}
