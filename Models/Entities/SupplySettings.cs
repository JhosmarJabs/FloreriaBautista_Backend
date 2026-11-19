namespace FloreriaBautista.Models.Entities;

public class SupplySettings
{
    public int      Id                          { get; set; } = 1;
    public int      DiaAbastecimiento           { get; set; } = 1; // 0=Dom..6=Sáb
    public TimeOnly HoraCutoff                  { get; set; } = new(12, 0);
    public int      DiaCutoff                   { get; set; } = 6;
    public int      ToleranciaUnidades          { get; set; } = 5;
    public decimal  ToleranciaPorcentaje        { get; set; } = 0.10m;
    public bool     UsarToleranciaAbsoluta      { get; set; } = true;
    public decimal  MargenSeguridadPorcentaje   { get; set; } = 0.10m;
    public int      DiasMinimosAnticipacion     { get; set; } = 7;
    public bool     PermitirVentaSinReceta      { get; set; } = true;
    public DateTime ActualizadoEn               { get; set; } = DateTime.UtcNow;
}
