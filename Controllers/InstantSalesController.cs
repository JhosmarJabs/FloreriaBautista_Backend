using FloreriaBautista.Extensions;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.InstantSales;
using FloreriaBautista.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FloreriaBautista.Data;

namespace FloreriaBautista.Controllers;

[ApiController]
[Tags("Venta Instantanea")]
[Route("api/solicitudes-venta-instantanea")]
[Authorize(Roles = "CLIENTE")]
public class InstantSalesController : ControllerBase
{
    private readonly IInstantSaleService _instantSaleService;
    private readonly AppDbContext _context;

    public InstantSalesController(
        IInstantSaleService instantSaleService,
        AppDbContext context)
    {
        _instantSaleService = instantSaleService;
        _context = context;
    }

    /// <summary>
    /// POST /api/solicitudes-venta-instantanea
    /// Crea una solicitud de venta instantanea. Puede resultar en PENDIENTE
    /// o RECHAZADA (automatica) segun el producto y la disponibilidad.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CreateInstantSaleRequestDto request)
    {
        var customerId = await ObtenerCustomerIdAsync();
        if (customerId == null)
            return Unauthorized(ApiResponseDto<string>.Fail(
                "No se encontro un perfil de cliente asociado a tu cuenta."));

        var resultado = await _instantSaleService.CrearSolicitudAsync(customerId.Value, request);
        return Ok(ApiResponseDto<InstantSaleResponseDto>.Ok(resultado, "Solicitud procesada."));
    }

    /// <summary>
    /// GET /api/solicitudes-venta-instantanea/{id}
    /// Consulta el estado de una solicitud (respaldo sin tiempo real).
    /// Solo el cliente dueno puede consultarla.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obtener(Guid id)
    {
        var customerId = await ObtenerCustomerIdAsync();
        if (customerId == null)
            return Unauthorized(ApiResponseDto<string>.Fail(
                "No se encontro un perfil de cliente asociado a tu cuenta."));

        var resultado = await _instantSaleService.ObtenerSolicitudAsync(customerId.Value, id);
        return Ok(ApiResponseDto<InstantSaleResponseDto>.Ok(resultado));
    }

    // ── Helper: obtener CustomerId del usuario autenticado ─────────────
    private async Task<Guid?> ObtenerCustomerIdAsync()
    {
        var userId = User.UsuarioId();
        if (userId == null) return null;

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.UserId == userId.Value);
        return customer?.Id;
    }
}
