namespace FloreriaBautista.Models.DTOs.Promotions;

public class PromotionDto
{
    public Guid      Id                   { get; set; }
    public string    Nombre               { get; set; } = string.Empty;
    public string?   Codigo               { get; set; }
    public string    Tipo                 { get; set; } = string.Empty;
    public decimal   Valor                { get; set; }
    public decimal   MinimoCompra         { get; set; }
    public string    Estado               { get; set; } = string.Empty;
    public DateOnly? FechaInicio          { get; set; }
    public DateOnly? FechaFin             { get; set; }
    public int?      MaxUsos              { get; set; }
    public int       UsosActuales         { get; set; }
    public bool      AplicarATodaLaTienda { get; set; }
    public DateTime  CreadoEn             { get; set; }
}

public class SavePromotionRequestDto
{
    public string    Nombre               { get; set; } = string.Empty;
    public string?   Codigo               { get; set; }
    public string    Tipo                 { get; set; } = "PORCENTAJE";
    public decimal   Valor                { get; set; }
    public decimal   MinimoCompra         { get; set; } = 0;
    public string    Estado               { get; set; } = "ACTIVO";
    public DateOnly? FechaInicio          { get; set; }
    public DateOnly? FechaFin             { get; set; }
    public int?      MaxUsos              { get; set; }
    public bool      AplicarATodaLaTienda { get; set; } = true;
}
