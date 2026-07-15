using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.QuickSale;
using FloreriaBautista.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Controllers;

// Plantillas de botones rápidos para el POS (Venta Rápida).
// Compartidas entre todos los empleados: se guardan en BD, no en localStorage,
// para que cualquier terminal/dispositivo vea la misma configuración.
[ApiController]
[Tags("Privado o Cliente")]
[Route("api/quick-sale-templates")]
[Authorize(Roles = "ADMIN,EMPLEADO")]
public class QuickSaleTemplatesController : ControllerBase
{
    private readonly AppDbContext _context;
    public QuickSaleTemplatesController(AppDbContext context) => _context = context;

    // GET /api/quick-sale-templates
    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var templates = await _context.QuickSaleTemplates
            .Include(t => t.Items).ThenInclude(i => i.Product)
            .OrderBy(t => t.Orden).ThenBy(t => t.CreadoEn)
            .ToListAsync();

        return Ok(ApiResponseDto<List<QuickSaleTemplateDto>>.Ok(templates.Select(MapToDto).ToList()));
    }

    // POST /api/quick-sale-templates (Crear)
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] SaveQuickSaleTemplateRequestDto request)
    {
        var error = Validar(request);
        if (error != null) return BadRequest(ApiResponseDto<object>.Fail(error));

        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var existentes = await _context.Products.Where(p => productIds.Contains(p.Id)).Select(p => p.Id).ToListAsync();
        var faltantes = productIds.Except(existentes).ToList();
        if (faltantes.Count > 0)
            return BadRequest(ApiResponseDto<object>.Fail("Uno o más productos ya no existen en el catálogo."));

        var template = new QuickSaleTemplate
        {
            Id            = Guid.NewGuid(),
            Nombre        = request.Nombre.Trim(),
            Descripcion   = string.IsNullOrWhiteSpace(request.Descripcion) ? null : request.Descripcion.Trim(),
            Icono         = string.IsNullOrWhiteSpace(request.Icono) ? "Sparkles" : request.Icono,
            Orden         = request.Orden,
            CreadoEn      = DateTime.UtcNow,
            ActualizadoEn = DateTime.UtcNow,
        };

        template.Items = request.Items.Select((i, idx) => new QuickSaleTemplateItem
        {
            Id                  = Guid.NewGuid(),
            QuickSaleTemplateId = template.Id,
            ProductId           = i.ProductId,
            Icono               = string.IsNullOrWhiteSpace(i.Icono) ? "Sparkles" : i.Icono,
            Color               = string.IsNullOrWhiteSpace(i.Color) ? "blue" : i.Color,
            Orden               = idx,
        }).ToList();

        _context.QuickSaleTemplates.Add(template);
        await _context.SaveChangesAsync();

        // Recargar con el producto incluido para poder mapear nombre/precio en la respuesta
        await _context.Entry(template).Collection(t => t.Items).Query().Include(i => i.Product).LoadAsync();

        return Ok(ApiResponseDto<QuickSaleTemplateDto>.Ok(MapToDto(template), "Plantilla creada correctamente."));
    }

    // POST /api/quick-sale-templates/{id} (Actualizar)
    [HttpPost("{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] SaveQuickSaleTemplateRequestDto request)
    {
        var template = await _context.QuickSaleTemplates
            .Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (template == null) return NotFound(ApiResponseDto<object>.Fail("Plantilla no encontrada."));

        var error = Validar(request);
        if (error != null) return BadRequest(ApiResponseDto<object>.Fail(error));

        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var existentes = await _context.Products.Where(p => productIds.Contains(p.Id)).Select(p => p.Id).ToListAsync();
        var faltantes = productIds.Except(existentes).ToList();
        if (faltantes.Count > 0)
            return BadRequest(ApiResponseDto<object>.Fail("Uno o más productos ya no existen en el catálogo."));

        template.Nombre        = request.Nombre.Trim();
        template.Descripcion   = string.IsNullOrWhiteSpace(request.Descripcion) ? null : request.Descripcion.Trim();
        template.Icono         = string.IsNullOrWhiteSpace(request.Icono) ? "Sparkles" : request.Icono;
        template.Orden         = request.Orden;
        template.ActualizadoEn = DateTime.UtcNow;

        // Reemplaza todos los items: es más simple y confiable que hacer diff,
        // dado que el frontend siempre manda la lista completa de botones.
        _context.QuickSaleTemplateItems.RemoveRange(template.Items);
        template.Items = request.Items.Select((i, idx) => new QuickSaleTemplateItem
        {
            Id                  = Guid.NewGuid(),
            QuickSaleTemplateId = template.Id,
            ProductId           = i.ProductId,
            Icono               = string.IsNullOrWhiteSpace(i.Icono) ? "Sparkles" : i.Icono,
            Color               = string.IsNullOrWhiteSpace(i.Color) ? "blue" : i.Color,
            Orden               = idx,
        }).ToList();

        await _context.SaveChangesAsync();

        await _context.Entry(template).Collection(t => t.Items).Query().Include(i => i.Product).LoadAsync();

        return Ok(ApiResponseDto<QuickSaleTemplateDto>.Ok(MapToDto(template), "Plantilla actualizada correctamente."));
    }

    // POST /api/quick-sale-templates/{id}/eliminar
    [HttpPost("{id:guid}/eliminar")]
    public async Task<IActionResult> Eliminar(Guid id)
    {
        var template = await _context.QuickSaleTemplates.FindAsync(id);
        if (template == null) return NotFound(ApiResponseDto<object>.Fail("Plantilla no encontrada."));

        _context.QuickSaleTemplates.Remove(template);
        await _context.SaveChangesAsync();
        return Ok(ApiResponseDto<object>.Ok(null!, "Plantilla eliminada correctamente."));
    }

    private static string? Validar(SaveQuickSaleTemplateRequestDto r)
    {
        if (string.IsNullOrWhiteSpace(r.Nombre)) return "El nombre de la plantilla es obligatorio.";
        if (r.Items != null && r.Items.Any(i => i.ProductId == Guid.Empty))
            return "Uno de los botones no tiene un producto válido asociado.";
        return null;
    }

    private static QuickSaleTemplateDto MapToDto(QuickSaleTemplate t) => new()
    {
        Id          = t.Id,
        Nombre      = t.Nombre,
        Descripcion = t.Descripcion,
        Icono       = t.Icono,
        Orden       = t.Orden,
        Items = t.Items
            .OrderBy(i => i.Orden)
            .Select(i => new QuickSaleTemplateItemDto
            {
                Id        = i.Id,
                ProductId = i.ProductId,
                Nombre    = i.Product.Nombre,
                Precio    = i.Product.PrecioBase,
                Icono     = i.Icono,
                Color     = i.Color,
            }).ToList()
    };
}
