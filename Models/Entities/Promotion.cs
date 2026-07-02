namespace FloreriaBautista.Models.Entities;

public class Promotion
{
    public Guid     Id                   { get; set; }
    public string   Nombre               { get; set; } = string.Empty;
    public string?  Codigo               { get; set; }
    public string   Tipo                 { get; set; } = "PORCENTAJE"; // PORCENTAJE / MONTO_FIJO / COMBO
    public decimal  Valor                { get; set; } = 0;
    public decimal  MinimoCompra         { get; set; } = 0;
    public string   Estado               { get; set; } = "ACTIVO"; // ACTIVO / INACTIVO / PROGRAMADO
    public DateOnly? FechaInicio         { get; set; }
    public DateOnly? FechaFin            { get; set; }
    public int?     MaxUsos              { get; set; }
    public int      UsosActuales         { get; set; } = 0;
    public bool     AplicarATodaLaTienda { get; set; } = true;
    public DateTime CreadoEn             { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadoEn        { get; set; } = DateTime.UtcNow;
}
