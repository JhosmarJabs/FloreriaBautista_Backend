using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Promotions;
using FloreriaBautista.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Controllers.Admin;

[ApiController]
[Tags("1. Catálogo y Diseño")]
[Route("api/admin/discounts")]
[Authorize(Roles = "ADMIN")]
public class AdminDescuentosController : ControllerBase
{
    private readonly AppDbContext _context;
    public AdminDescuentosController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var items = await _context.Descuentos
            .Include(d => d.Categoria)
            .Include(d => d.Catalogo)
            .OrderByDescending(d => d.CreadoEn)
            .Select(d => MapToDto(d))
            .ToListAsync();

        return Ok(ApiResponseDto<List<DescuentoDto>>.Ok(items));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detalle(Guid id)
    {
        var item = await _context.Descuentos
            .Include(d => d.Categoria)
            .Include(d => d.Catalogo)
            .FirstOrDefaultAsync(d => d.Id == id);
        if (item == null) return NotFound(ApiResponseDto<object>.Fail("Descuento no encontrado."));
        return Ok(ApiResponseDto<DescuentoDto>.Ok(MapToDto(item)));
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] SaveDescuentoRequestDto request)
    {
        var error = Validar(request);
        if (error != null) return BadRequest(ApiResponseDto<object>.Fail(error));

        var desc = new Descuento
        {
            Id                = Guid.NewGuid(),
            Nombre            = request.Nombre.Trim(),
            TipoRegla         = request.TipoRegla.ToUpper(),
            MontoMinimoCompra = request.MontoMinimoCompra,
            CategoriaId       = request.CategoriaId,
            CatalogoId        = request.CatalogoId,
            TipoValor         = request.TipoValor.ToUpper(),
            Valor             = request.Valor,
            FechaInicio       = request.FechaInicio,
            FechaFin          = request.FechaFin,
            Activo            = request.Activo,
            CreadoEn          = DateTime.UtcNow,
            ActualizadoEn     = DateTime.UtcNow
        };

        _context.Descuentos.Add(desc);
        await _context.SaveChangesAsync();

        var saved = await _context.Descuentos
            .Include(d => d.Categoria).Include(d => d.Catalogo)
            .FirstAsync(d => d.Id == desc.Id);

        return Ok(ApiResponseDto<DescuentoDto>.Ok(MapToDto(saved), "Descuento creado correctamente."));
    }

    [HttpPost("{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] SaveDescuentoRequestDto request)
    {
        var item = await _context.Descuentos
            .Include(d => d.Categoria).Include(d => d.Catalogo)
            .FirstOrDefaultAsync(d => d.Id == id);
        if (item == null) return NotFound(ApiResponseDto<object>.Fail("Descuento no encontrado."));

        var error = Validar(request);
        if (error != null) return BadRequest(ApiResponseDto<object>.Fail(error));

        item.Nombre            = request.Nombre.Trim();
        item.TipoRegla         = request.TipoRegla.ToUpper();
        item.MontoMinimoCompra = request.MontoMinimoCompra;
        item.CategoriaId       = request.CategoriaId;
        item.CatalogoId        = request.CatalogoId;
        item.TipoValor         = request.TipoValor.ToUpper();
        item.Valor             = request.Valor;
        item.FechaInicio       = request.FechaInicio;
        item.FechaFin          = request.FechaFin;
        item.Activo            = request.Activo;
        item.ActualizadoEn     = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(ApiResponseDto<DescuentoDto>.Ok(MapToDto(item), "Descuento actualizado correctamente."));
    }

    [HttpPost("{id:guid}/eliminar")]
    public async Task<IActionResult> Eliminar(Guid id)
    {
        var item = await _context.Descuentos.FindAsync(id);
        if (item == null) return NotFound(ApiResponseDto<object>.Fail("Descuento no encontrado."));

        _context.Descuentos.Remove(item);
        await _context.SaveChangesAsync();
        return Ok(ApiResponseDto<object>.Ok(null!, "Descuento eliminado correctamente."));
    }

    private static string? Validar(SaveDescuentoRequestDto r)
    {
        if (string.IsNullOrWhiteSpace(r.Nombre)) return "El nombre del descuento es obligatorio.";

        var regla = r.TipoRegla?.ToUpper();
        if (regla != "MONTO_MINIMO" && regla != "CATEGORIA" && regla != "CATALOGO")
            return "Tipo de regla inválido. Use: MONTO_MINIMO, CATEGORIA o CATALOGO.";

        var tipoValor = r.TipoValor?.ToUpper();
        if (tipoValor != "PORCENTAJE" && tipoValor != "MONTO_FIJO")
            return "Tipo de valor inválido. Use: PORCENTAJE o MONTO_FIJO.";

        if (tipoValor == "PORCENTAJE" && (r.Valor <= 0 || r.Valor > 100))
            return "El porcentaje de descuento debe estar entre 1 y 100.";
        if (tipoValor == "MONTO_FIJO" && r.Valor <= 0)
            return "El monto de descuento debe ser mayor a 0.";

        if (regla == "MONTO_MINIMO" && (!r.MontoMinimoCompra.HasValue || r.MontoMinimoCompra <= 0))
            return "El monto mínimo de compra es obligatorio para reglas de tipo MONTO_MINIMO.";
        if (regla == "CATEGORIA" && !r.CategoriaId.HasValue)
            return "Debe seleccionar una categoría para reglas de tipo CATEGORIA.";
        if (regla == "CATALOGO" && !r.CatalogoId.HasValue)
            return "Debe seleccionar un catálogo para reglas de tipo CATALOGO.";

        if (r.FechaInicio.HasValue && r.FechaFin.HasValue && r.FechaFin < r.FechaInicio)
            return "La fecha de fin no puede ser anterior a la fecha de inicio.";

        return null;
    }

    private static DescuentoDto MapToDto(Descuento d) => new()
    {
        Id                = d.Id,
        Nombre            = d.Nombre,
        TipoRegla         = d.TipoRegla,
        MontoMinimoCompra = d.MontoMinimoCompra,
        CategoriaId       = d.CategoriaId,
        CategoriaNombre   = d.Categoria?.Nombre,
        CatalogoId        = d.CatalogoId,
        CatalogoNombre    = d.Catalogo?.Nombre,
        TipoValor         = d.TipoValor,
        Valor             = d.Valor,
        FechaInicio       = d.FechaInicio,
        FechaFin          = d.FechaFin,
        Activo            = d.Activo,
        CreadoEn          = d.CreadoEn
    };
}
