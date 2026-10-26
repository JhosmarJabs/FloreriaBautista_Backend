using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Eventos;

namespace FloreriaBautista.Services.Eventos;

public interface ISolicitudEventoService
{
    /// <summary>Crea la solicitud del cliente. Lanza <c>AppException</c> (400) si algo no es válido.</summary>
    Task<SolicitudEventoDto> CrearAsync(Guid customerId, CrearSolicitudEventoDto dto);

    Task<PagedResultDto<SolicitudEventoDto>> ListarAsync(string? estado, int page, int size);

    /// <summary>Cambia el estado. <c>AppException</c> si no está en catálogo; <c>NotFoundException</c> si no existe.</summary>
    Task<SolicitudEventoDto> CambiarEstadoAsync(Guid id, string estado);
}
