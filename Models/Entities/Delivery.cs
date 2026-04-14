namespace FloreriaBautista.Models.Entities;

public class Delivery
{
    public Guid      Id                   { get; set; }
    public Guid      OrderId              { get; set; }
    public Guid?     RepartidorId         { get; set; }
    public DateOnly  FechaProgramada      { get; set; }
    public TimeOnly? HoraProgramada       { get; set; }
    public string    EstadoEntrega        { get; set; } = "PROGRAMADA";
    public DateTime? FechaReal            { get; set; }
    public string?   EvidenciaFotografica { get; set; }
    public string?   FirmaReceptor        { get; set; }
    public string?   Notas                { get; set; }

    public Order Order       { get; set; } = null!;
    public User?  Repartidor { get; set; }
}
