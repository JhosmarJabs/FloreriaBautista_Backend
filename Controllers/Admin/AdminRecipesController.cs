using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Controllers.Admin;

[ApiController]
[Tags("1. Catálogo y Diseño")]
[Route("api/admin/recipes")]
[Authorize(Roles = "ADMIN")]
public class AdminRecipesController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminRecipesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> ListarTodas()
    {
        var recipes = await _context.ProductRecipes
            .Include(r => r.Product)
            .Include(r => r.InventoryItem)
            .Select(r => new { r.Id, r.ProductId, ProductName = r.Product.Nombre, r.InventoryItemId, ItemName = r.InventoryItem.Nombre, r.CantidadRequerida })
            .ToListAsync();
        return Ok(ApiResponseDto<object>.Ok(recipes));
    }

    [HttpPost("{productId:guid}")]
    public async Task<IActionResult> GuardarReceta(Guid productId, [FromBody] List<ProductRecipe> request)
    {
        var product = await _context.Products.FindAsync(productId);
        if (product == null) return NotFound(ApiResponseDto<string>.Fail("Producto no encontrado"));

        var existentes = await _context.ProductRecipes.Where(pr => pr.ProductId == productId).ToListAsync();
        _context.ProductRecipes.RemoveRange(existentes);

        foreach (var item in request)
        {
            item.Id = Guid.NewGuid();
            item.ProductId = productId;
            _context.ProductRecipes.Add(item);
        }

        await _context.SaveChangesAsync();
        return Ok(ApiResponseDto<string>.Ok("Receta guardada", "Receta de producto actualizada correctamente."));
    }

    [HttpGet("{productId:guid}/suggested-price")]
    public async Task<IActionResult> CalcularPrecioSugerido(Guid productId)
    {
        var recipeItems = await _context.ProductRecipes
            .Include(r => r.InventoryItem)
            .Where(r => r.ProductId == productId)
            .ToListAsync();

        // Como InventoryItem no maneja costo financiero explícito en el modelo base, calculamos un factor multiplicador ficticio
        // de 5 unidades monetarias base por cada insumo (InventoryItem) configurado que sume al costo.
        decimal costoBaseMateriales = recipeItems
            .Where(r => r.InventoryItem.SumaAlCosto)
            .Sum(r => r.CantidadRequerida * 5m); 
            
        decimal sugerido = costoBaseMateriales * 1.5m; // 50% de margen base

        return Ok(ApiResponseDto<object>.Ok(new { ProductId = productId, CostoMateriales = costoBaseMateriales, PrecioSugerido = sugerido }, "Cálculo referencial exitoso."));
    }
}
