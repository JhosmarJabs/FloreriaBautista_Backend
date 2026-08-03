using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Inventory;

namespace FloreriaBautista.Services.Interfaces;

public interface IInventoryService
{
    Task<PagedResultDto<InventoryItemDto>>     ListarAsync(string? sucursal, bool? bajoMinimo, string? busqueda, int page, int size);
    Task<InventoryItemDto>                     ObtenerAsync(Guid id);
    Task<InventoryItemDto>                     CrearAsync(CreateInventoryItemDto request);
    Task<InventoryItemDto>                     ActualizarAsync(Guid id, UpdateInventoryItemDto request);
    Task                                       EliminarAsync(Guid id);
    Task<InventoryMovementDto>                 RegistrarMovimientoAsync(RegisterMovementRequestDto request, Guid usuarioId);
    Task<PagedResultDto<InventoryMovementDto>> ListarMovimientosAsync(Guid? inventoryItemId, int page, int size);
    
    // Historial y Predicción
    Task<InventoryHistoryDto> ObtenerHistorialAsync(Guid inventoryItemId);
    Task<SupplyForecastDto>   ObtenerPrediccionSurtidoAsync(Guid inventoryItemId);
    Task<List<SupplyReplenishmentItemDto>> ObtenerReabastecimientoAsync();
    Task                      RegistrarSnapshotDiarioAsync();
    Task<InventoryKpisDto>    ObtenerKpisAsync();
    Task<InventoryItemDto?>   ResolverCoincidenciaInsumoAsync(string termino);

    // Consumo reciente (derivado de OrderItems x ProductRecipe) para priorizar insumos por venta real
    Task<Dictionary<Guid,int>> ObtenerConsumoRecienteAsync(int dias = 30);
}
