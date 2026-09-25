using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Orders;

namespace FloreriaBautista.Services.Interfaces;

public interface IOrderService
{
    Task<OrderResponseDto>               CrearPedidoClienteAsync(Guid userId, CreateOrderRequestDto request);
    Task<OrderResponseDto>               CrearPedidoFisicoAsync(CreatePhysicalOrderRequestDto request, Guid? atendidoPorUsuarioId = null);
    Task<PagedResultDto<OrderSummaryDto>> ListarMisPedidosAsync(Guid userId, int page, int size);
    Task<OrderResponseDto>               ObtenerMiPedidoAsync(Guid userId, Guid orderId);
    Task<OrderResponseDto>               CambiarEstadoAsync(Guid orderId, UpdateOrderStatusRequestDto request, List<string> rolesUsuario, Guid? restringirAEmpleado = null);
    Task<OrderResponseDto>               RegistrarPagoAsync(Guid orderId, RegisterPaymentRequestDto request, Guid? restringirAEmpleado = null);
    Task<PagedResultDto<OrderSummaryDto>> ListarAdminAsync(string? estado, DateOnly? desde, DateOnly? hasta, int page, int size, bool archivado = false, bool requierenCierre = false);
    Task<IndexResultDto<OrderSummaryDto>> ListarDeltaAdminAsync(DateTime desde);
    Task<OrderResponseDto>               ObtenerAdminAsync(Guid orderId);

    // ── Empleado ──────────────────────────────────────────────────
    // Sin parámetros de fecha ni de dueño: el alcance lo fija el servicio a
    // partir del usuario del token y del día en curso de la tienda.
    Task<PagedResultDto<OrderSummaryDto>> ListarEmpleadoAsync(Guid usuarioId, string? estado, int page, int size);
    Task<OrderResponseDto>                ObtenerParaEmpleadoAsync(Guid usuarioId, Guid orderId);
}
