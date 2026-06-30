using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Products;
using FloreriaBautista.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FloreriaBautista.Controllers.Admin;

[ApiController]
[Tags("1. Catálogo y Diseño")]
[Route("api/admin/products")]
[Authorize(Roles = "ADMIN")]
public class AdminProductsController : ControllerBase
{
    private readonly IProductService _productService;
    public AdminProductsController(IProductService productService) => _productService = productService;

    // GET /api/admin/products/kpis
    [HttpGet("kpis")]
    public async Task<IActionResult> ObtenerKpis()
    {
        var kpis = await _productService.ObtenerKpisAsync();
        return Ok(ApiResponseDto<ProductKpisDto>.Ok(kpis));
    }

    // GET /api/admin/products?busqueda=&estado=ACTIVO&page=1&size=20
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string? busqueda,
        [FromQuery] string? estado,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var resultado = await _productService.ListarAdminAsync(busqueda, estado, page, size);
        return Ok(ApiResponseDto<PagedResultDto<ProductSummaryDto>>.Ok(resultado));
    }

    // GET /api/admin/products/{productId}
    [HttpGet("{productId:guid}")]
    public async Task<IActionResult> Detalle(Guid productId)
    {
        var producto = await _productService.ObtenerAdminAsync(productId);
        return Ok(ApiResponseDto<ProductResponseDto>.Ok(producto));
    }

    // POST /api/admin/products
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CreateProductRequestDto request)
    {
        var producto = await _productService.CrearAsync(request);
        return Ok(ApiResponseDto<ProductResponseDto>.Ok(producto, "Producto creado correctamente."));
    }

    // POST /api/admin/products/{productId:guid} (Actualizar / Borrado lógico)
    [HttpPost("{productId:guid}")]
    public async Task<IActionResult> Actualizar(Guid productId,[FromBody] UpdateProductRequestDto request)
    {
        var producto = await _productService.ActualizarAsync(productId, request);
        return Ok(ApiResponseDto<ProductResponseDto>.Ok(producto, "Producto actualizado correctamente."));
    }
}

