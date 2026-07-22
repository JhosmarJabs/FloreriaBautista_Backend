using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Products;
using FloreriaBautista.Models.DTOs.Recommendations;
using FloreriaBautista.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FloreriaBautista.Controllers;

[ApiController]
[Tags("Público")]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly IProductService        _productService;
    private readonly IRecommendationService _recommendationService;

    public ProductsController(IProductService productService, IRecommendationService recommendationService)
    {
        _productService         = productService;
        _recommendationService  = recommendationService;
    }

    // GET /api/products?busqueda=rosa&categoria=flores&page=1&size=20
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string? busqueda,
        [FromQuery] string? categoria,
        [FromQuery] string? catalogo,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var resultado = await _productService.ListarPublicosAsync(busqueda, categoria, catalogo, page, size);
        return Ok(ApiResponseDto<PagedResultDto<ProductSummaryDto>>.Ok(resultado));
    }

    // GET /api/products/{productId}
    [HttpGet("{productId:guid}")]
    public async Task<IActionResult> Detalle(Guid productId)
    {
        var producto = await _productService.ObtenerPublicoAsync(productId);
        return Ok(ApiResponseDto<ProductResponseDto>.Ok(producto));
    }

    // GET /api/products/{productId}/recomendados
    // Propuesta 2 (modelos predictivos): "suele comprarse junto con..." basado en reglas de
    // asociación minadas de pedidos anteriores. Fallback a más vendidos si no hay regla.
    [HttpGet("{productId:guid}/recomendados")]
    public async Task<IActionResult> Recomendados(Guid productId, [FromQuery] int top = 4)
    {
        var recomendados = await _recommendationService.ObtenerRecomendadosAsync([productId], top);
        return Ok(ApiResponseDto<List<ProductRecommendationDto>>.Ok(recomendados));
    }

    // GET /api/products/recomendados?ids=guid1,guid2,guid3
    // Misma recomendación pero para todo el carrito (varios productos de contexto a la vez).
    [HttpGet("recomendados")]
    public async Task<IActionResult> RecomendadosPorCarrito([FromQuery] string ids, [FromQuery] int top = 4)
    {
        var productosEnContexto = (ids ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(id => Guid.TryParse(id, out var g) ? g : (Guid?)null)
            .Where(g => g.HasValue)
            .Select(g => g!.Value)
            .ToList();

        var recomendados = await _recommendationService.ObtenerRecomendadosAsync(productosEnContexto, top);
        return Ok(ApiResponseDto<List<ProductRecommendationDto>>.Ok(recomendados));
    }
}
