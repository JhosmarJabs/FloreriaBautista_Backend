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
[Route("api/admin/offers")]
[Authorize(Roles = "ADMIN")]
public class AdminOfertasController : ControllerBase
{
    private readonly AppDbContext _context;
    public AdminOfertasController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var items = await _context.Ofertas
            .Include(o => o.Producto)
            .OrderByDescending(o => o.CreadoEn)
            .Select(o => MapToDto(o))
            .ToListAsync();

        return Ok(ApiResponseDto<List<OfertaDto>>.Ok(items));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detalle(Guid id)
    {
        var item = await _context.Ofertas.Include(o => o.Producto).FirstOrDefaultAsync(o => o.Id == id);
        if (item == null) return NotFound(ApiResponseDto<object>.Fail("Oferta no encontrada."));
        return Ok(ApiResponseDto<OfertaDto>.Ok(MapToDto(item)));
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] SaveOfertaRequestDto request)
    {
        var error = await Validar(request, null);
        if (error != null) return BadRequest(ApiResponseDto<object>.Fail(error));

        var producto = await _context.Products.FindAsync(request.ProductoId);
        if (producto == null)
            return BadRequest(ApiResponseDto<object>.Fail("Producto no encontrado."));

        var oferta = new Oferta
        {
            Id            = Guid.NewGuid(),
            ProductoId    = request.ProductoId,
            PrecioOferta  = request.PrecioOferta,
            FechaInicio   = request.FechaInicio,
            FechaFin      = request.FechaFin,
            Activo        = request.Activo,
            CreadoEn      = DateTime.UtcNow,
            ActualizadoEn = DateTime.UtcNow,
            Producto      = producto
        };

        _context.Ofertas.Add(oferta);
        await _context.SaveChangesAsync();
        return Ok(ApiResponseDto<OfertaDto>.Ok(MapToDto(oferta), "Oferta creada correctamente."));
    }

    [HttpPost("{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] SaveOfertaRequestDto request)
    {
        var item = await _context.Ofertas.Include(o => o.Producto).FirstOrDefaultAsync(o => o.Id == id);
        if (item == null) return NotFound(ApiResponseDto<object>.Fail("Oferta no encontrada."));

        var error = await Validar(request, id);
        if (error != null) return BadRequest(ApiResponseDto<object>.Fail(error));

        item.ProductoId    = request.ProductoId;
        item.PrecioOferta  = request.PrecioOferta;
        item.FechaInicio   = request.FechaInicio;
        item.FechaFin      = request.FechaFin;
        item.Activo        = request.Activo;
        item.ActualizadoEn = DateTime.UtcNow;

        if (item.ProductoId != request.ProductoId)
        {
            var producto = await _context.Products.FindAsync(request.ProductoId);
            if (producto == null) return BadRequest(ApiResponseDto<object>.Fail("Producto no encontrado."));
            item.Producto = producto;
        }

        await _context.SaveChangesAsync();
        return Ok(ApiResponseDto<OfertaDto>.Ok(MapToDto(item), "Oferta actualizada correctamente."));
    }

    [HttpPost("{id:guid}/eliminar")]
    public async Task<IActionResult> Eliminar(Guid id)
    {
        var item = await _context.Ofertas.FindAsync(id);
        if (item == null) return NotFound(ApiResponseDto<object>.Fail("Oferta no encontrada."));

        _context.Ofertas.Remove(item);
        await _context.SaveChangesAsync();
        return Ok(ApiResponseDto<object>.Ok(null!, "Oferta eliminada correctamente."));
    }

    private async Task<string?> Validar(SaveOfertaRequestDto r, Guid? excludeId)
    {
        if (r.PrecioOferta <= 0) return "El precio de oferta debe ser mayor a 0.";
        if (r.FechaInicio.HasValue && r.FechaFin.HasValue && r.FechaFin < r.FechaInicio)
            return "La fecha de fin no puede ser anterior a la fecha de inicio.";

        if (r.Activo)
        {
            var existeOtraActiva = await _context.Ofertas
                .AnyAsync(o => o.ProductoId == r.ProductoId
                    && o.Activo
                    && (excludeId == null || o.Id != excludeId));
            if (existeOtraActiva)
                return "Ya existe una oferta activa para este producto. Desactívela primero.";
        }

        return null;
    }

    private static OfertaDto MapToDto(Oferta o) => new()
    {
        Id             = o.Id,
        ProductoId     = o.ProductoId,
        ProductoNombre = o.Producto?.Nombre ?? "",
        ProductoImagen = o.Producto?.ImagenUrl,
        PrecioBase     = o.Producto?.PrecioBase ?? 0,
        PrecioOferta   = o.PrecioOferta,
        FechaInicio    = o.FechaInicio,
        FechaFin       = o.FechaFin,
        Activo         = o.Activo,
        CreadoEn       = o.CreadoEn
    };
}
