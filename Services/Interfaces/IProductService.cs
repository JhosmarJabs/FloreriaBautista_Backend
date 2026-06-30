using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Products;

namespace FloreriaBautista.Services.Interfaces;

public interface IProductService
{
    Task<PagedResultDto<ProductSummaryDto>> ListarPublicosAsync(string? busqueda, string? categoria, string? catalogo, int page, int size);
    Task<ProductResponseDto>               ObtenerPublicoAsync(Guid id);
    Task<ProductResponseDto>               ObtenerAdminAsync(Guid id);
    Task<PagedResultDto<ProductSummaryDto>> ListarAdminAsync(string? busqueda, string? estado, int page, int size);
    Task<ProductResponseDto>               CrearAsync(CreateProductRequestDto request);
    Task<ProductResponseDto>               ActualizarAsync(Guid id, UpdateProductRequestDto request);
    Task                                   EliminarAsync(Guid id);
    Task<ProductKpisDto>                   ObtenerKpisAsync();
}
