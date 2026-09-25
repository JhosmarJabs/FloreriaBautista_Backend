using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Promotions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Controllers;

[ApiController]
[Tags("Público")]
[Route("api/offers")]
public class OfertasPublicController : ControllerBase
{
    private readonly AppDbContext _context;
    public OfertasPublicController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> ListarVigentes()
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        var items = await _context.Ofertas
            .Include(o => o.Producto)
            .Where(o => o.Activo
                && o.Producto.Estado == "ACTIVO"
                && o.Producto.Activo
                && (!o.FechaInicio.HasValue || o.FechaInicio <= hoy)
                && (!o.FechaFin.HasValue    || o.FechaFin   >= hoy))
            .OrderByDescending(o => o.CreadoEn)
            .Select(o => new OfertaPublicaDto
            {
                Id             = o.Id,
                ProductoId     = o.ProductoId,
                ProductoNombre = o.Producto.Nombre,
                ProductoImagen = o.Producto.ImagenUrl,
                ProductoTipo   = o.Producto.Tipo,
                PrecioBase     = o.Producto.PrecioBase,
                PrecioOferta   = o.PrecioOferta,
                PorcentajeDesc = o.Producto.PrecioBase > 0
                    ? (int)Math.Round((1 - o.PrecioOferta / o.Producto.PrecioBase) * 100)
                    : 0,
                FechaFin       = o.FechaFin
            })
            .ToListAsync();

        return Ok(ApiResponseDto<List<OfertaPublicaDto>>.Ok(items));
    }
}
