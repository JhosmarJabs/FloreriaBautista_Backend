namespace FloreriaBautista.Models.Entities;

public class Notification
{
    public Guid      Id                       { get; set; }
    public Guid?     DestinatarioUsuarioId    { get; set; }
    public Guid?     DestinatarioCustomerId   { get; set; }
    public string    Tipo                     { get; set; } = string.Empty;
    public string    Titulo                   { get; set; } = string.Empty;
    public string    Mensaje                  { get; set; } = string.Empty;
    public string?   EntidadTipo              { get; set; }
    public Guid?     EntidadId                { get; set; }
    public bool      Leida                    { get; set; } = false;
    public DateTime? LeidaEn                  { get; set; }
    public DateTime  CreadaEn                 { get; set; } = DateTime.UtcNow;

    public User?     Usuario  { get; set; }
    public Customer? Customer { get; set; }
}
