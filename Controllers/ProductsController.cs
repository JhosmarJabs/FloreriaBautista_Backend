using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Products;
using FloreriaBautista.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FloreriaBautista.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    public ProductsController(IProductService productService) => _productService = productService;

    // GET /api/products?busqueda=rosa&categoria=flores&page=1&size=20
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string? busqueda,
        [FromQuery] string? categoria,
        [FromQuery] string? coleccion,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var resultado = await _productService.ListarPublicosAsync(busqueda, categoria, coleccion, page, size);
        return Ok(ApiResponseDto<PagedResultDto<ProductSummaryDto>>.Ok(resultado));
    }

    // GET /api/products/{productId}
    [HttpGet("{productId:guid}")]
    public async Task<IActionResult> Detalle(Guid productId)
    {
        var producto = await _productService.ObtenerPublicoAsync(productId);
        return Ok(ApiResponseDto<ProductResponseDto>.Ok(producto));
    }
}
