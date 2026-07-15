using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Products;
using FloreriaBautista.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FloreriaBautista.Controllers;

[ApiController]
[Tags("Privado o Cliente")]
[Route("api/employee/products")]
[Authorize(Roles = "ADMIN,EMPLEADO")]
public class EmployeeProductsController : ControllerBase
{
    private readonly IProductService _productService;
    public EmployeeProductsController(IProductService productService) => _productService = productService;

    // GET /api/employee/products?busqueda=rosa&categoria=flores&catalogo=&page=1&size=20
    // Catálogo para registrar pedidos físicos: incluye productos "SOLO_SUCURSAL"
    // que el listado público oculta, pero sin exponer datos internos de admin.
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string? busqueda,
        [FromQuery] string? categoria,
        [FromQuery] string? catalogo,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var resultado = await _productService.ListarParaEmpleadoAsync(busqueda, categoria, catalogo, page, size);
        return Ok(ApiResponseDto<PagedResultDto<ProductSummaryDto>>.Ok(resultado));
    }
}
