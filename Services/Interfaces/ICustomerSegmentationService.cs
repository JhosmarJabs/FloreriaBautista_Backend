using FloreriaBautista.Models.DTOs.Analytics;

namespace FloreriaBautista.Services.Interfaces;

public interface ICustomerSegmentationService
{
    // Propuesta 3: segmentación de clientes por RFM (Recencia, Frecuencia, Monto) + clustering.
    Task<RecalcularSegmentosResultDto>   RecalcularSegmentosAsync();
    Task<List<CustomerSegmentGroupDto>>  ObtenerSegmentosAsync();
}
