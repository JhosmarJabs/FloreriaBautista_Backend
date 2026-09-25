using FloreriaBautista.Models.DTOs.Promotions;

namespace FloreriaBautista.Services.Interfaces;

public interface IPricingService
{
    Task<PricingBreakdownDto> CalcularTotalAsync(
        List<PricingItemDto> items, string? codigoCupon = null);
}
