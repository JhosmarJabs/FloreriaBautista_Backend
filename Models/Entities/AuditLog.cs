namespace FloreriaBautista.Models.Entities;

public class AuditLog
{
    public Guid     Id         { get; set; }
    public Guid?    UsuarioId  { get; set; }
    public string   Accion     { get; set; } = string.Empty;
    public string   Entidad    { get; set; } = string.Empty;
    public string?  EntidadId  { get; set; }
    public string?  Detalles   { get; set; } // JSON antes/después
    public DateTime FechaHora  { get; set; } = DateTime.UtcNow;

    public User? Usuario { get; set; }
}
