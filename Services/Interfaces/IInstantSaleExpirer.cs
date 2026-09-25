using FloreriaBautista.Models.DTOs.InstantSales;

namespace FloreriaBautista.Services.Interfaces;

/// <summary>
/// Regla de escalamiento, expiracion y liberacion de solicitudes de venta
/// instantanea. La ejecutan dos disparadores: <see cref="Scheduler.InstantSaleExpirerService"/>
/// (cada 15-30s, en segundo plano) y el endpoint manual
/// <c>POST /api/admin/orders/expirar-solicitudes-instantaneas</c>.
/// </summary>
public interface IInstantSaleExpirer
{
    /// <param name="usuarioId">Admin que forzo la pasada; null si la ejecuto el scheduler.</param>
    Task<ExpiracionResultDto> RevisarPendientesAsync(Guid? usuarioId = null);
}
