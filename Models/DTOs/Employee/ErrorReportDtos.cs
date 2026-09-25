using System.ComponentModel.DataAnnotations;

namespace FloreriaBautista.Models.DTOs.Employee;

public class CreateErrorReportRequestDto
{
    /// <summary>VENTA / GASTO / CORTE.</summary>
    [Required] public string TipoRegistro { get; set; } = string.Empty;

    [Required] public Guid   RegistroId   { get; set; }

    [Required] [MinLength(10, ErrorMessage = "Explica el error con al menos 10 caracteres.")]
    [MaxLength(1000)]
    public string Motivo { get; set; } = string.Empty;
}

public class ErrorReportDto
{
    public Guid      Id               { get; set; }
    public string    TipoRegistro     { get; set; } = string.Empty;
    public Guid      RegistroId       { get; set; }
    public string    Motivo           { get; set; } = string.Empty;
    public string    Estado           { get; set; } = string.Empty;
    public DateTime  FechaHora        { get; set; }
    public DateTime? FechaResolucion  { get; set; }
    public string?   ResolucionAccion { get; set; }
    public string?   ResolucionNota   { get; set; }

    /// <summary>Quién reportó. Solo se llena para el admin.</summary>
    public string?   Empleado         { get; set; }
}

/// <summary>Resolución del admin. El empleado nunca toca este DTO.</summary>
public class ResolveErrorReportRequestDto
{
    /// <summary>CORREGIDO / CANCELADO / INVALIDADO / ELIMINADO / SIN_CAMBIO.</summary>
    [Required] public string  Accion { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Nota { get; set; }

    /// <summary>Marca el reporte como RECHAZADO en vez de RESUELTO: el admin
    /// revisó y concluyó que no procedía ninguna corrección.</summary>
    public bool Rechazar { get; set; }
}
