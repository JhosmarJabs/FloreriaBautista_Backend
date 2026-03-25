namespace FloreriaBautista.Models.DTOs.Audit;

public class AuditLogDto
{
    public Guid     Id            { get; set; }
    public Guid?    UsuarioId     { get; set; }
    public string?  UsuarioNombre { get; set; }
    public string?  UsuarioCorreo { get; set; }
    public string   Accion        { get; set; } = string.Empty;
    public string   Entidad       { get; set; } = string.Empty;
    public string?  EntidadId     { get; set; }
    public string?  Detalles      { get; set; }
    public DateTime FechaHora     { get; set; }
}
