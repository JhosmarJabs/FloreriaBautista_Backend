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
[Route("api/admin/promotions")]
[Authorize(Roles = "ADMIN")]
public class AdminPromotionsController : ControllerBase
{
    private readonly AppDbContext _context;
    public AdminPromotionsController(AppDbContext context) => _context = context;

    // GET /api/admin/promotions
    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var items = await _context.Promotions
            .OrderByDescending(p => p.CreadoEn)
            .Select(p => MapToDto(p))
            .ToListAsync();

        return Ok(ApiResponseDto<List<PromotionDto>>.Ok(items));
    }

    // GET /api/admin/promotions/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detalle(Guid id)
    {
        var item = await _context.Promotions.FindAsync(id);
        if (item == null) return NotFound(ApiResponseDto<object>.Fail("Promoción no encontrada."));
        return Ok(ApiResponseDto<PromotionDto>.Ok(MapToDto(item)));
    }

    // POST /api/admin/promotions (Crear)
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] SavePromotionRequestDto request)
    {
        var error = Validar(request);
        if (error != null) return BadRequest(ApiResponseDto<object>.Fail(error));

        if (!string.IsNullOrWhiteSpace(request.Codigo))
        {
            var codigoDuplicado = await _context.Promotions
                .AnyAsync(p => p.Codigo == request.Codigo!.ToUpper());
            if (codigoDuplicado)
                return BadRequest(ApiResponseDto<object>.Fail("Ya existe una promoción con ese código."));
        }

        var promo = new Promotion
        {
            Id                   = Guid.NewGuid(),
            Nombre               = request.Nombre.Trim(),
            Codigo               = string.IsNullOrWhiteSpace(request.Codigo) ? null : request.Codigo.Trim().ToUpper(),
            Tipo                 = request.Tipo.ToUpper(),
            Valor                = request.Valor,
            MinimoCompra         = request.MinimoCompra,
            Estado               = request.Estado.ToUpper(),
            FechaInicio          = request.FechaInicio,
            FechaFin             = request.FechaFin,
            MaxUsos              = request.MaxUsos,
            AplicarATodaLaTienda = request.AplicarATodaLaTienda,
            CreadoEn             = DateTime.UtcNow,
            ActualizadoEn        = DateTime.UtcNow
        };

        _context.Promotions.Add(promo);
        await _context.SaveChangesAsync();
        return Ok(ApiResponseDto<PromotionDto>.Ok(MapToDto(promo), "Promoción creada correctamente."));
    }

    // POST /api/admin/promotions/{id} (Actualizar)
    [HttpPost("{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] SavePromotionRequestDto request)
    {
        var item = await _context.Promotions.FindAsync(id);
        if (item == null) return NotFound(ApiResponseDto<object>.Fail("Promoción no encontrada."));

        var error = Validar(request);
        if (error != null) return BadRequest(ApiResponseDto<object>.Fail(error));

        var codigoNuevo = string.IsNullOrWhiteSpace(request.Codigo) ? null : request.Codigo.Trim().ToUpper();
        if (codigoNuevo != null)
        {
            var codigoDuplicado = await _context.Promotions
                .AnyAsync(p => p.Id != id && p.Codigo == codigoNuevo);
            if (codigoDuplicado)
                return BadRequest(ApiResponseDto<object>.Fail("Ya existe una promoción con ese código."));
        }

        item.Nombre               = request.Nombre.Trim();
        item.Codigo               = codigoNuevo;
        item.Tipo                 = request.Tipo.ToUpper();
        item.Valor                = request.Valor;
        item.MinimoCompra         = request.MinimoCompra;
        item.Estado               = request.Estado.ToUpper();
        item.FechaInicio          = request.FechaInicio;
        item.FechaFin             = request.FechaFin;
        item.MaxUsos              = request.MaxUsos;
        item.AplicarATodaLaTienda = request.AplicarATodaLaTienda;
        item.ActualizadoEn        = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(ApiResponseDto<PromotionDto>.Ok(MapToDto(item), "Promoción actualizada correctamente."));
    }

    // DELETE /api/admin/promotions/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Eliminar(Guid id)
    {
        var item = await _context.Promotions.FindAsync(id);
        if (item == null) return NotFound(ApiResponseDto<object>.Fail("Promoción no encontrada."));

        _context.Promotions.Remove(item);
        await _context.SaveChangesAsync();
        return Ok(ApiResponseDto<object>.Ok(null!, "Promoción eliminada correctamente."));
    }

    private static string? Validar(SavePromotionRequestDto r)
    {
        if (string.IsNullOrWhiteSpace(r.Nombre)) return "El nombre de la promoción es obligatorio.";

        var tipo = r.Tipo?.ToUpper();
        if (tipo != "PORCENTAJE" && tipo != "MONTO_FIJO" && tipo != "COMBO")
            return "Tipo inválido. Use: PORCENTAJE, MONTO_FIJO o COMBO.";

        var estado = r.Estado?.ToUpper();
        if (estado != "ACTIVO" && estado != "INACTIVO" && estado != "PROGRAMADO")
            return "Estado inválido. Use: ACTIVO, INACTIVO o PROGRAMADO.";

        if (tipo != "COMBO" && string.IsNullOrWhiteSpace(r.Codigo))
            return "El código de cupón es obligatorio para promociones de tipo Porcentaje o Monto Fijo.";

        if (tipo == "PORCENTAJE" && (r.Valor <= 0 || r.Valor > 100))
            return "El porcentaje de descuento debe estar entre 1 y 100.";

        if (tipo == "MONTO_FIJO" && r.Valor <= 0)
            return "El monto de descuento debe ser mayor a 0.";

        if (r.FechaInicio.HasValue && r.FechaFin.HasValue && r.FechaFin < r.FechaInicio)
            return "La fecha de fin no puede ser anterior a la fecha de inicio.";

        return null;
    }

    private static PromotionDto MapToDto(Promotion p) => new()
    {
        Id                   = p.Id,
        Nombre               = p.Nombre,
        Codigo               = p.Codigo,
        Tipo                 = p.Tipo,
        Valor                = p.Valor,
        MinimoCompra         = p.MinimoCompra,
        Estado               = p.Estado,
        FechaInicio          = p.FechaInicio,
        FechaFin             = p.FechaFin,
        MaxUsos              = p.MaxUsos,
        UsosActuales         = p.UsosActuales,
        AplicarATodaLaTienda = p.AplicarATodaLaTienda,
        CreadoEn             = p.CreadoEn
    };
}
