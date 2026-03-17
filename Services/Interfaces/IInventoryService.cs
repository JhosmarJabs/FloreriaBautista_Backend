using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Inventory;

namespace FloreriaBautista.Services.Interfaces;

public interface IInventoryService
{
    Task<PagedResultDto<InventoryItemDto>>    ListarAsync(string? sucursal, bool? bajoMinimo, int page, int size);
    Task<InventoryItemDto>                    ObtenerPorProductoAsync(Guid productId);
    Task<InventoryMovementDto>                RegistrarMovimientoAsync(RegisterMovementRequestDto request, Guid usuarioId);
    Task<PagedResultDto<InventoryMovementDto>> ListarMovimientosAsync(Guid? productId, int page, int size);
}
