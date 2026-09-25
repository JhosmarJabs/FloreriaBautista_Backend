using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Promotions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Controllers;

[ApiController]
[Tags("Público")]
[Route("api/discounts")]
public class DescuentosPublicController : ControllerBase
{
    private readonly AppDbContext _context;
    public DescuentosPublicController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> ListarVigentes()
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        var items = await _context.Descuentos
            .Include(d => d.Categoria)
            .Include(d => d.Catalogo)
            .Where(d => d.Activo
                && (!d.FechaInicio.HasValue || d.FechaInicio <= hoy)
                && (!d.FechaFin.HasValue    || d.FechaFin   >= hoy))
            .OrderByDescending(d => d.CreadoEn)
            .Select(d => new DescuentoPublicoDto
            {
                Id                = d.Id,
                Nombre            = d.Nombre,
                TipoRegla         = d.TipoRegla,
                MontoMinimoCompra = d.MontoMinimoCompra,
                CategoriaNombre   = d.Categoria != null ? d.Categoria.Nombre : null,
                CatalogoNombre    = d.Catalogo != null ? d.Catalogo.Nombre : null,
                TipoValor         = d.TipoValor,
                Valor             = d.Valor,
                FechaFin          = d.FechaFin
            })
            .ToListAsync();

        return Ok(ApiResponseDto<List<DescuentoPublicoDto>>.Ok(items));
    }
}
