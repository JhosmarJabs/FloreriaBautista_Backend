namespace FloreriaBautista.Models.Entities;

public class Oferta
{
    public Guid     Id            { get; set; }
    public Guid     ProductoId    { get; set; }
    public decimal  PrecioOferta  { get; set; }
    public DateOnly? FechaInicio  { get; set; }
    public DateOnly? FechaFin     { get; set; }
    public bool     Activo        { get; set; } = true;
    public DateTime CreadoEn      { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;

    public Product Producto { get; set; } = null!;
}
