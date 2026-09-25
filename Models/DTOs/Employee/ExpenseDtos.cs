using System.ComponentModel.DataAnnotations;

namespace FloreriaBautista.Models.DTOs.Employee;

public class CreateExpenseRequestDto
{
    [Required] [MaxLength(200)]
    public string  Concepto  { get; set; } = string.Empty;

    [Required] [Range(0.01, double.MaxValue, ErrorMessage = "El importe debe ser mayor a 0.")]
    public decimal Importe   { get; set; }

    [MaxLength(500)]
    public string? Notas     { get; set; }
}

public class ExpenseDto
{
    public Guid     Id               { get; set; }
    public DateTime FechaHora        { get; set; }
    public string   Concepto         { get; set; } = string.Empty;
    public string   Categoria        { get; set; } = string.Empty;
    public decimal  Importe          { get; set; }
    public string   Estado           { get; set; } = string.Empty;

    /// <summary>True cuando ya entró en un corte cerrado. La app lo usa para
    /// explicar por qué el registro ya no admite reporte de error.</summary>
    public bool     IncluidoEnCorte  { get; set; }

    /// <summary>Estado del reporte de error abierto sobre este gasto, si lo hay.
    /// Null = el empleado todavía no ha reportado nada.</summary>
    public string?  EstadoReporte    { get; set; }

    /// <summary>Nombre del empleado. Solo se llena en las consultas del admin;
    /// en las del empleado va vacío porque ya sabe que son suyos.</summary>
    public string?  Empleado         { get; set; }
}
