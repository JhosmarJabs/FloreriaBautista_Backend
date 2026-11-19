using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.QuickSale;
using FloreriaBautista.Models.DTOs.Sync;
using FloreriaBautista.Services;
using FloreriaBautista.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Controllers.Admin;

[ApiController]
[Tags("1. Administración")]
[Route("api/admin")]
[Authorize(Roles = "ADMIN")]
public class AdminMobileSyncController : ControllerBase
{
    private readonly IProductService   _products;
    private readonly IOrderService     _orders;
    private readonly IInventoryService _inventory;
    private readonly AppDbContext       _context;

    public AdminMobileSyncController(
        IProductService   products,
        IOrderService     orders,
        IInventoryService inventory,
        AppDbContext       context)
    {
        _products  = products;
        _orders    = orders;
        _inventory = inventory;
        _context   = context;
    }

    /// <summary>
    /// Endpoint consolidado de sync para la app móvil del admin.
    /// Sin <c>desde</c>: full sync (catálogo, inventario, pedidos activos, plantillas activas).
    /// Con <c>desde</c>: delta sync (solo lo modificado desde esa marca).
    /// </summary>
    [HttpGet("mobile-sync")]
    public async Task<IActionResult> Sync([FromQuery] DateTime? desde)
    {
        var sincronizadoEn = DateTime.UtcNow;
        var esDelta = desde.HasValue;

        var resultado = new AdminMobileSyncResponseDto
        {
            SincronizadoEn = sincronizadoEn.ToString("o"),
            EsDelta        = esDelta
        };

        if (esDelta)
        {
            var desdeUtc = FechaHelper.DesdeDelta(desde);

            var productsDelta = await _products.ListarDeltaAsync(desdeUtc);
            resultado.Productos = productsDelta.Items;

            var inventoryDelta = await _inventory.ListarDeltaAsync(desdeUtc);
            resultado.Inventario = inventoryDelta.Items;

            var ordersDelta = await _orders.ListarDeltaAdminAsync(desdeUtc);
            resultado.Pedidos = ordersDelta.Items;

            resultado.Plantillas = await _context.QuickSaleTemplates
                .Include(t => t.Items).ThenInclude(i => i.Product)
                .Where(t => t.ActualizadoEn > desdeUtc)
                .OrderBy(t => t.Orden).ThenBy(t => t.CreadoEn)
                .Select(t => new QuickSaleTemplateDto
                {
                    Id          = t.Id,
                    Nombre      = t.Nombre,
                    Descripcion = t.Descripcion,
                    Icono       = t.Icono,
                    Orden       = t.Orden,
                    Activa      = t.Activa,
                    Items       = t.Items.OrderBy(i => i.Orden).Select(i => new QuickSaleTemplateItemDto
                    {
                        Id        = i.Id,
                        ProductId = i.ProductId,
                        Nombre    = i.Product!.Nombre,
                        Precio    = i.Product.PrecioBase,
                        Icono     = i.Icono,
                        Color     = i.Color,
                        Cantidad  = i.CantidadPreset
                    }).ToList()
                })
                .ToListAsync();
        }
        else
        {
            var allProducts = await _products.ListarDeltaAsync(DateTime.MinValue);
            resultado.Productos = allProducts.Items;

            var allInventory = await _inventory.ListarDeltaAsync(DateTime.MinValue);
            resultado.Inventario = allInventory.Items;

            resultado.Pedidos = (await _orders.ListarDeltaAdminAsync(DateTime.MinValue)).Items
                .Where(o => !o.Archivado).ToList();

            resultado.Plantillas = await _context.QuickSaleTemplates
                .Include(t => t.Items).ThenInclude(i => i.Product)
                .Where(t => t.Activa)
                .OrderBy(t => t.Orden).ThenBy(t => t.CreadoEn)
                .Select(t => new QuickSaleTemplateDto
                {
                    Id          = t.Id,
                    Nombre      = t.Nombre,
                    Descripcion = t.Descripcion,
                    Icono       = t.Icono,
                    Orden       = t.Orden,
                    Activa      = t.Activa,
                    Items       = t.Items.OrderBy(i => i.Orden).Select(i => new QuickSaleTemplateItemDto
                    {
                        Id        = i.Id,
                        ProductId = i.ProductId,
                        Nombre    = i.Product!.Nombre,
                        Precio    = i.Product.PrecioBase,
                        Icono     = i.Icono,
                        Color     = i.Color,
                        Cantidad  = i.CantidadPreset
                    }).ToList()
                })
                .ToListAsync();
        }

        return Ok(ApiResponseDto<AdminMobileSyncResponseDto>.Ok(resultado));
    }
}
