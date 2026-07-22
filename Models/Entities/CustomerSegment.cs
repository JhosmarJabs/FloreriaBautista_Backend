namespace FloreriaBautista.Models.Entities;

// Resultado de la segmentación RFM + clustering para un cliente. Se recalcula
// periódicamente y sobrescribe el registro anterior del mismo CustomerId.
public class CustomerSegment
{
    public Guid Id             { get; set; }
    public Guid CustomerId     { get; set; }
    public string Grupo        { get; set; } = string.Empty; // VIP / FRECUENTE / OCASIONAL / INACTIVO
    public int RecenciaDias    { get; set; }
    public int FrecuenciaPedidos { get; set; }
    public decimal MontoTotal  { get; set; }
    public DateTime FechaCalculo { get; set; } = DateTime.UtcNow;

    public Customer Customer { get; set; } = null!;
}
