using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace FloreriaBautista.Controllers.Admin;

[ApiController]
[Tags("1. Catálogo y Diseño")]
[Route("api/admin/recipes")]
[Authorize(Roles = "ADMIN,EMPLEADO")]
public class AdminFlowersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<AdminFlowersController> _logger;

    public AdminFlowersController(AppDbContext context, ILogger<AdminFlowersController> logger)
    {
        _context = context;
        _logger  = logger;
    }

    // GET /api/admin/recipes/product/{productId} — receta de un producto
    [HttpGet("product/{productId:guid}")]
    public async Task<IActionResult> ObtenerReceta(Guid productId)
    {
        var receta = await _context.ProductRecipes
            .Include(r => r.InventoryItem)
            .Where(r => r.ProductId == productId)
            .Select(r => new ProductRecipeDto
            {
                Id                = r.Id,
                InventoryItemId   = r.InventoryItemId,
                NombreInsumo      = r.InventoryItem.Nombre,
                UnidadMedida      = r.InventoryItem.UnidadMedida,
                SumaAlCosto       = r.InventoryItem.SumaAlCosto,
                CantidadRequerida = r.CantidadRequerida
            })
            .ToListAsync();

        return Ok(ApiResponseDto<List<ProductRecipeDto>>.Ok(receta));
    }

    // POST /api/admin/recipes/product/{productId} — reemplazar receta completa de un producto
    [HttpPost("product/{productId:guid}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> ActualizarReceta(
        Guid productId, [FromBody] UpdateProductRecipeDto request)
    {
        var producto = await _context.Products.FindAsync(productId)
            ?? throw new NotFoundException("Producto", productId);

        var anteriores = await _context.ProductRecipes
            .Where(r => r.ProductId == productId).ToListAsync();
        _context.RemoveRange(anteriores);

        foreach (var item in request.Insumos)
        {
            var insumo = await _context.InventoryItems.FindAsync(item.InventoryItemId)
                ?? throw new AppException($"Insumo '{item.InventoryItemId}' no encontrado.");

            _context.ProductRecipes.Add(new ProductRecipe
            {
                Id              = Guid.NewGuid(),
                ProductId       = productId,
                InventoryItemId = item.InventoryItemId,
                CantidadRequerida = item.CantidadRequerida
            });
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Receta actualizada para producto {Id}: {Count} insumos",
            productId, request.Insumos.Count);

        return Ok(ApiResponseDto<object>.Ok(null!,
            $"Receta actualizada: {request.Insumos.Count} insumos asignados."));
    }
}

public class ProductRecipeDto
{
    public Guid    Id                { get; set; }
    public Guid    InventoryItemId   { get; set; }
    public string  NombreInsumo      { get; set; } = string.Empty;
    public string? UnidadMedida      { get; set; }
    public bool    SumaAlCosto       { get; set; }
    public int     CantidadRequerida { get; set; }
}

public class UpdateProductRecipeDto
{
    public List<RecipeItemDto> Insumos { get; set; } = [];
}

public class RecipeItemDto
{
    [Required] public Guid InventoryItemId   { get; set; }
    [Range(1, int.MaxValue)]
    public int             CantidadRequerida { get; set; } = 1;
}
