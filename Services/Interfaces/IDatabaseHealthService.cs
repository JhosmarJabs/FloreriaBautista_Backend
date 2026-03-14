using FloreriaBautista.Models.DTOs.Database;

namespace FloreriaBautista.Services.Interfaces;

public interface IDatabaseHealthService
{
    /// <summary>Verifica conexión, versión y estado general de la BD.</summary>
    Task<HealthResponseDto> VerificarConexionAsync();
}
