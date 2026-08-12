using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.SupplyOrders;

namespace FloreriaBautista.Services.Interfaces;

public interface ISupplyOrderService
{
    Task<PagedResultDto<SupplyOrderListItemDto>> ListarAsync(
        string? estado, DateTime? desde, DateTime? hasta, int page, int size);

    Task<SupplyOrderDetailDto> ObtenerAsync(Guid id);
    Task<SupplyOrderDetailDto> CrearAsync(CreateSupplyOrderDto request, Guid usuarioId);
    Task<SupplyOrderDetailDto> ActualizarAsync(Guid id, UpdateSupplyOrderDto request, Guid usuarioId);
    Task<SupplyOrderDetailDto> EnviarAsync(Guid id, Guid usuarioId);
    Task<SupplyOrderDetailDto> RegistrarRecepcionAsync(Guid id, ReceiveSupplyOrderDto request, Guid usuarioId);
    Task<SupplyOrderDetailDto> CancelarAsync(Guid id, CancelSupplyOrderDto request, Guid usuarioId);
}
