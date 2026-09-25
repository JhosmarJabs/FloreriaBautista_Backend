using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.InstantSales;

namespace FloreriaBautista.Services.Interfaces;

/// <summary>
/// Operaciones sobre solicitudes de venta instantanea.
/// Bloque 2: creacion de solicitudes y calculo de limite efectivo.
/// Bloque 3: decision (aceptar/rechazar), listado admin.
/// </summary>
public interface IInstantSaleService
{
    // ── Bloque 2: Creacion y consulta del cliente ─────────────────

    /// <summary>
    /// Crea una solicitud de venta instantanea. Aplica rechazos automaticos
    /// por producto no habilitado o por cantidad excedida del limite efectivo.
    /// </summary>
    Task<InstantSaleResponseDto> CrearSolicitudAsync(Guid customerId, CreateInstantSaleRequestDto request);

    /// <summary>
    /// Consulta el estado de una solicitud (respaldo sin tiempo real).
    /// Solo el cliente dueno puede consultarla.
    /// </summary>
    Task<InstantSaleResponseDto> ObtenerSolicitudAsync(Guid customerId, Guid solicitudId);

    /// <summary>
    /// Calcula el limite efectivo de venta instantanea para un producto.
    /// Reutilizable por Bloques 3 y 4.
    /// </summary>
    Task<int> CalcularLimiteEfectivoAsync(Guid productId);

    // ── Bloque 3: Decision y listado admin ────────────────────────

    /// <summary>Acepta una solicitud PENDIENTE. Lock optimista: 409 si ya fue decidida.</summary>
    Task<SolicitudVentaInstantaneaDto> AceptarAsync(Guid solicitudId, Guid decisorUsuarioId);

    /// <summary>Rechaza una solicitud PENDIENTE. Lock optimista: 409 si ya fue decidida.</summary>
    Task<SolicitudVentaInstantaneaDto> RechazarAsync(Guid solicitudId, Guid decisorUsuarioId, string? motivoRechazo);

    /// <summary>Listado admin con filtro por estado, paginado.</summary>
    Task<PagedResultDto<SolicitudVentaInstantaneaDto>> ListarAdminAsync(
        string? estado, int page, int size);

    /// <summary>Obtiene una solicitud por id para el admin.</summary>
    Task<SolicitudVentaInstantaneaDto> ObtenerAsync(Guid solicitudId);
}
