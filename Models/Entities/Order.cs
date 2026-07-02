namespace FloreriaBautista.Models.Entities;

public class Order
{
    public Guid     Id                           { get; set; }
    public Guid?    IdLocalOffline               { get; set; }
    public Guid     CustomerId                   { get; set; }
    public string   TipoPedido                   { get; set; } = string.Empty; // INSTANTANEO / ANTICIPADO
    public string   Canal                        { get; set; } = string.Empty; // FISICO / WEB / TELEFONO
    public string   EstadoPedido                 { get; set; } = "PENDIENTE_VALIDACION";
    public string   DireccionEntregaCalle        { get; set; } = string.Empty;
    public string   DireccionEntregaColonia      { get; set; } = string.Empty;
    public string   DireccionEntregaMunicipio    { get; set; } = string.Empty;
    public string   DireccionEntregaEstado       { get; set; } = string.Empty;
    public string?  DireccionEntregaCp           { get; set; }
    public string?  DireccionEntregaReferencias  { get; set; }
    public decimal? CostoEnvio                   { get; set; }
    public DateTime FechaCreacion               { get; set; } = DateTime.UtcNow;
    public DateOnly FechaEntrega                { get; set; }
    public TimeOnly? HoraEntrega               { get; set; }
    public decimal  Total                        { get; set; } = 0;
    public decimal  SaldoPendiente               { get; set; } = 0;
    public DateTime? SincronizadoEn             { get; set; }
    public string?  Notas                        { get; set; }
    public bool     Archivado                    { get; set; } = false;
    public DateTime? ArchivadoEn                 { get; set; }

    public Customer              Customer   { get; set; } = null!;
    public ICollection<OrderItem> OrderItems { get; set; } = [];
    public ICollection<Payment>  Payments   { get; set; } = [];
    public Delivery?             Delivery   { get; set; }
}
