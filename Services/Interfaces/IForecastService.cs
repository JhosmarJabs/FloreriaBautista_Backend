using FloreriaBautista.Models.DTOs.Inventory;

namespace FloreriaBautista.Services.Interfaces;

public interface IForecastService
{
    Task<ForecastResultDto> GenerarRecomendacionAsync(Guid inventoryItemId);
}
