namespace FloreriaBautista.Models.DTOs.InstantSales;

/// <summary>Resumen de una pasada del expirador de solicitudes instantaneas.</summary>
public class ExpiracionResultDto
{
    public int Escaladas            { get; set; }
    public int ExpiradasSinRespuesta { get; set; }
    public int ReservasLiberadas    { get; set; }
}
