namespace FloreriaBautista.Models.Entities;

public class FlowerMovement
{
    public Guid     Id         { get; set; }
    public Guid     FlowerId   { get; set; }
    public string   Tipo       { get; set; } = string.Empty;
    public int      Cantidad   { get; set; }
    public string?  Motivo     { get; set; }
    public Guid     UsuarioId  { get; set; }
    public DateTime FechaHora  { get; set; } = DateTime.UtcNow;

    public Flower Flower  { get; set; } = null!;
    public User   Usuario { get; set; } = null!;
}
