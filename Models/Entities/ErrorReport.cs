namespace FloreriaBautista.Models.Entities;

/// <summary>
/// Reporte de error: la única vía que tiene un empleado para corregir algo que ya
/// registró. No modifica el registro original — lo marca para que el admin decida.
///
/// <see cref="RegistroId"/> es un Guid suelto y no una FK, porque apunta a tres
/// tablas distintas según <see cref="TipoRegistro"/>. La validación de que el
/// registro existe y pertenece a quien reporta se hace en el servicio.
/// </summary>
public class ErrorReport
{
    public Guid     Id           { get; set; }

    /// <summary>Empleado que levanta el reporte.</summary>
    public Guid     UsuarioId    { get; set; }

    /// <summary>VENTA / GASTO / CORTE.</summary>
    public string   TipoRegistro { get; set; } = string.Empty;

    public Guid     RegistroId   { get; set; }
    public string   Motivo       { get; set; } = string.Empty;

    /// <summary>PENDIENTE / EN_REVISION / RESUELTO / RECHAZADO.</summary>
    public string   Estado       { get; set; } = "PENDIENTE";

    public DateTime FechaHora    { get; set; } = DateTime.UtcNow;

    // ── Resolución (solo la escribe el admin) ─────────────────────
    public Guid?     ResueltoPorUsuarioId { get; set; }
    public DateTime? FechaResolucion      { get; set; }

    /// <summary>CORREGIDO / CANCELADO / INVALIDADO / ELIMINADO / SIN_CAMBIO.</summary>
    public string?   ResolucionAccion     { get; set; }
    public string?   ResolucionNota       { get; set; }

    public User  Usuario     { get; set; } = null!;
    public User? ResueltoPor { get; set; }
}
