using FloreriaBautista.Models.DTOs.Inventory;

namespace FloreriaBautista.Services.Interfaces;

public interface IAvailabilityService
{
    Task<SupplyAvailabilityDto>         ObtenerDisponibilidadInsumoAsync(Guid inventoryItemId);
    Task<ProductAvailabilityDto>        ObtenerDisponibilidadProductoAsync(Guid productId);
    Task<SaleValidationResult>          ValidarVentaInstantaneaAsync(Guid productId, int cantidad);
    Task<AdvanceSaleValidationResult>   ValidarVentaAnticipadaAsync(Guid productId, int cantidad, DateOnly fechaEntrega);
    Task<SupplyCycleDto>                ObtenerCicloAbastecimientoAsync();
    Task RegistrarDemandaNoCubiertaAsync(Guid inventoryItemId, Guid? productId, int solicitada, int disponible, string origen);
}
