using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Orders;

namespace FloreriaBautista.Services.Interfaces;

public interface IOrderService
{
    Task<OrderResponseDto>               CrearPedidoClienteAsync(Guid userId, CreateOrderRequestDto request);
    Task<OrderResponseDto>               CrearPedidoFisicoAsync(CreatePhysicalOrderRequestDto request);
    Task<PagedResultDto<OrderSummaryDto>> ListarMisPedidosAsync(Guid userId, int page, int size);
    Task<OrderResponseDto>               ObtenerMiPedidoAsync(Guid userId, Guid orderId);
    Task<OrderResponseDto>               CambiarEstadoAsync(Guid orderId, UpdateOrderStatusRequestDto request, List<string> rolesUsuario);
    Task<OrderResponseDto>               RegistrarPagoAsync(Guid orderId, RegisterPaymentRequestDto request);
    Task<PagedResultDto<OrderSummaryDto>> ListarAdminAsync(string? estado, DateOnly? desde, DateOnly? hasta, int page, int size, bool archivado = false, bool requierenCierre = false);
    Task<OrderResponseDto>               ObtenerAdminAsync(Guid orderId);
}
