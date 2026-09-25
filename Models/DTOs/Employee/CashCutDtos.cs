using System.ComponentModel.DataAnnotations;

namespace FloreriaBautista.Models.DTOs.Employee;

/// <summary>
/// Lo que el empleado ve ANTES de cerrar: los números que el sistema calculó para
/// su día, para que los compare contra el dinero que tiene en la mano.
/// </summary>
public class CashCutPreviewDto
{
    public DateOnly Dia               { get; set; }
    public decimal  TotalVentas       { get; set; }
    public decimal  TotalEfectivo     { get; set; }
    public decimal  TotalGastos       { get; set; }
    public decimal  EfectivoEsperado  { get; set; }
    public int      PedidosContados   { get; set; }
    public int      GastosContados    { get; set; }

    /// <summary>True si el empleado ya cerró hoy. El corte es irrepetible: si ya
    /// está cerrado, la app debe mostrar el corte existente y no el formulario.</summary>
    public bool     YaCerrado         { get; set; }
}

public class CreateCashCutRequestDto
{
    [Required] [Range(0, double.MaxValue, ErrorMessage = "El efectivo declarado no puede ser negativo.")]
    public decimal EfectivoDeclarado { get; set; }

    [MaxLength(500)]
    public string? Notas             { get; set; }
}

public class CashCutDto
{
    public Guid     Id                { get; set; }
    public DateOnly FechaLocal        { get; set; }
    public DateTime FechaHoraCierre   { get; set; }
    public decimal  TotalVentas       { get; set; }
    public decimal  TotalEfectivo     { get; set; }
    public decimal  TotalGastos       { get; set; }
    public decimal  EfectivoEsperado  { get; set; }
    public decimal  EfectivoDeclarado { get; set; }
    public decimal  Diferencia        { get; set; }
    public int      PedidosContados   { get; set; }
    public int      GastosContados    { get; set; }
    public string   Estado            { get; set; } = string.Empty;
    public string?  Notas             { get; set; }
    public string?  EstadoReporte     { get; set; }
    public string?  Empleado          { get; set; }
}
