namespace FloreriaBautista.Models.DTOs.InstantSales;

/// <summary>Body del endpoint POST .../rechazar.</summary>
public class RechazarSolicitudRequestDto
{
    /// <summary>Texto libre del decisor; opcional.</summary>
    public string? MotivoRechazo { get; set; }
}
