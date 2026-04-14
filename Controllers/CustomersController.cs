using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Customers;
using FloreriaBautista.Models.DTOs.Orders;
using FloreriaBautista.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Controllers;

[ApiController]
[Tags("Privado o Cliente")]
[Route("api/customers")]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly AppDbContext _context;

    public CustomersController(AppDbContext context) => _context = context;

    // ──────────────────────────────────────────────────────────────────────────
    // GET /api/customers/search?q=nombre&page=1&size=20
    // Búsqueda de clientes (solo ADMIN/EMPLEADO)
    // ──────────────────────────────────────────────────────────────────────────
    [HttpGet("search")]
    [Authorize(Roles = "ADMIN,EMPLEADO")]
    public async Task<IActionResult> Buscar(
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var query = _context.Customers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var lower = q.ToLower();
            query = query.Where(c =>
                c.Nombre.ToLower().Contains(lower) ||
                (c.Apellido != null && c.Apellido.ToLower().Contains(lower)) ||
                c.Telefono.Contains(q) ||
                (c.Correo != null && c.Correo.ToLower().Contains(lower)));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(c => c.CreadoEn)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(c => new CustomerSummaryDto
            {
                Id           = c.Id,
                Nombre       = c.Nombre,
                Apellido     = c.Apellido,
                Telefono     = c.Telefono,
                Correo       = c.Correo,
                TipoCliente  = c.TipoCliente,
                TotalPedidos = c.Orders.Count,
                CreadoEn     = c.CreadoEn
            })
            .ToListAsync();

        var resultado = new PagedResultDto<CustomerSummaryDto>
        {
            Items        = items,
            Total        = total,
            Pagina       = page,
            TamanoPagina = size,
            TotalPaginas = (int)Math.Ceiling(total / (double)size)
        };

        return Ok(ApiResponseDto<PagedResultDto<CustomerSummaryDto>>.Ok(resultado));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /api/customers/physical
    // Alta rápida de cliente mostrador (ADMIN/EMPLEADO)
    // ──────────────────────────────────────────────────────────────────────────
    [HttpPost("physical")]
    [Authorize(Roles = "ADMIN,EMPLEADO")]
    public async Task<IActionResult> CrearFisico([FromBody] CreatePhysicalCustomerRequestDto request)
    {
        var cliente = new Customer
        {
            Id          = Guid.NewGuid(),
            TipoCliente = "MOSTRADOR",
            Nombre      = request.Nombre.Trim(),
            Apellido    = request.Apellido?.Trim(),
            Telefono    = request.Telefono.Trim(),
            Correo      = request.Correo?.Trim(),
            CreadoEn    = DateTime.UtcNow
        };

        _context.Customers.Add(cliente);
        await _context.SaveChangesAsync();

        var dto = new CustomerSummaryDto
        {
            Id          = cliente.Id,
            Nombre      = cliente.Nombre,
            Apellido    = cliente.Apellido,
            Telefono    = cliente.Telefono,
            Correo      = cliente.Correo,
            TipoCliente = cliente.TipoCliente,
            CreadoEn    = cliente.CreadoEn
        };

        return Ok(ApiResponseDto<CustomerSummaryDto>.Ok(dto, "Cliente mostrador creado correctamente."));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /api/customers/{customerId}/orders
    // Historial de pedidos de un cliente (ADMIN/EMPLEADO)
    // ──────────────────────────────────────────────────────────────────────────
    [HttpGet("{customerId:guid}/orders")]
    [Authorize(Roles = "ADMIN,EMPLEADO")]
    public async Task<IActionResult> PedidosDeCliente(
        Guid customerId,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var existe = await _context.Customers.AnyAsync(c => c.Id == customerId);
        if (!existe)
            return NotFound(ApiResponseDto<object>.Fail("Cliente no encontrado."));

        var query = _context.Orders.Where(o => o.CustomerId == customerId);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(o => o.FechaCreacion)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(o => new OrderSummaryDto
            {
                Id           = o.Id,
                EstadoPedido = o.EstadoPedido,
                FechaEntrega = o.FechaEntrega,
                Total        = o.Total,
                NombreCliente = o.Customer.Nombre,
                FechaCreacion = o.FechaCreacion
            })
            .ToListAsync();

        var resultado = new PagedResultDto<OrderSummaryDto>
        {
            Items        = items,
            Total        = total,
            Pagina       = page,
            TamanoPagina = size,
            TotalPaginas = (int)Math.Ceiling(total / (double)size)
        };

        return Ok(ApiResponseDto<PagedResultDto<OrderSummaryDto>>.Ok(resultado));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /api/customers/me/addresses
    // Mis direcciones guardadas (cliente logueado)
    // ──────────────────────────────────────────────────────────────────────────
    [HttpGet("me/addresses")]
    public async Task<IActionResult> MisDirecciones()
    {
        var customerId = await ObtenerCustomerIdAsync();
        if (customerId == null)
            return NotFound(ApiResponseDto<object>.Fail("No tienes un perfil de cliente asociado."));

        var addresses = await _context.Addresses
            .Where(a => a.CustomerId == customerId.Value)
            .OrderBy(a => a.CreadoEn)
            .Select(a => new AddressDto
            {
                Id          = a.Id,
                Etiqueta    = a.Etiqueta,
                Calle       = a.Calle,
                Colonia     = a.Colonia,
                Municipio   = a.Municipio,
                Estado      = a.Estado,
                Cp          = a.Cp,
                Referencias = a.Referencias,
                CreadoEn    = a.CreadoEn
            })
            .ToListAsync();

        return Ok(ApiResponseDto<List<AddressDto>>.Ok(addresses));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /api/customers/me/addresses
    // Guardar nueva dirección (cliente logueado)
    // ──────────────────────────────────────────────────────────────────────────
    [HttpPost("me/addresses")]
    public async Task<IActionResult> GuardarDireccion([FromBody] SaveAddressRequestDto request)
    {
        var customerId = await ObtenerCustomerIdAsync();
        if (customerId == null)
            return NotFound(ApiResponseDto<object>.Fail("No tienes un perfil de cliente asociado."));

        var address = new Address
        {
            Id          = Guid.NewGuid(),
            CustomerId  = customerId.Value,
            Etiqueta    = request.Etiqueta?.Trim(),
            Calle       = request.Calle.Trim(),
            Colonia     = request.Colonia.Trim(),
            Municipio   = request.Municipio.Trim(),
            Estado      = request.Estado.Trim(),
            Cp          = request.Cp?.Trim(),
            Referencias = request.Referencias?.Trim(),
            CreadoEn    = DateTime.UtcNow
        };

        _context.Addresses.Add(address);
        await _context.SaveChangesAsync();

        var dto = new AddressDto
        {
            Id          = address.Id,
            Etiqueta    = address.Etiqueta,
            Calle       = address.Calle,
            Colonia     = address.Colonia,
            Municipio   = address.Municipio,
            Estado      = address.Estado,
            Cp          = address.Cp,
            Referencias = address.Referencias,
            CreadoEn    = address.CreadoEn
        };

        return Ok(ApiResponseDto<AddressDto>.Ok(dto, "Dirección guardada correctamente."));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /api/customers/me/addresses/{addressId}
    // Actualizar dirección existente (cliente logueado)
    // ──────────────────────────────────────────────────────────────────────────
    [HttpPost("me/addresses/{addressId:guid}")]
    public async Task<IActionResult> ActualizarDireccion(Guid addressId, [FromBody] SaveAddressRequestDto request)
    {
        var customerId = await ObtenerCustomerIdAsync();
        if (customerId == null)
            return NotFound(ApiResponseDto<object>.Fail("No tienes un perfil de cliente asociado."));

        var address = await _context.Addresses
            .FirstOrDefaultAsync(a => a.Id == addressId && a.CustomerId == customerId.Value);

        if (address == null)
            return NotFound(ApiResponseDto<object>.Fail("Dirección no encontrada."));

        address.Etiqueta    = request.Etiqueta?.Trim();
        address.Calle       = request.Calle.Trim();
        address.Colonia     = request.Colonia.Trim();
        address.Municipio   = request.Municipio.Trim();
        address.Estado      = request.Estado.Trim();
        address.Cp          = request.Cp?.Trim();
        address.Referencias = request.Referencias?.Trim();

        await _context.SaveChangesAsync();

        var dto = new AddressDto
        {
            Id          = address.Id,
            Etiqueta    = address.Etiqueta,
            Calle       = address.Calle,
            Colonia     = address.Colonia,
            Municipio   = address.Municipio,
            Estado      = address.Estado,
            Cp          = address.Cp,
            Referencias = address.Referencias,
            CreadoEn    = address.CreadoEn
        };

        return Ok(ApiResponseDto<AddressDto>.Ok(dto, "Dirección actualizada correctamente."));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /api/customers/me/addresses/{addressId}/deactivate
    // Ocultar/eliminar dirección (cliente logueado)
    // ──────────────────────────────────────────────────────────────────────────
    [HttpPost("me/addresses/{addressId:guid}/deactivate")]
    public async Task<IActionResult> DesactivarDireccion(Guid addressId)
    {
        var customerId = await ObtenerCustomerIdAsync();
        if (customerId == null)
            return NotFound(ApiResponseDto<object>.Fail("No tienes un perfil de cliente asociado."));

        var address = await _context.Addresses
            .FirstOrDefaultAsync(a => a.Id == addressId && a.CustomerId == customerId.Value);

        if (address == null)
            return NotFound(ApiResponseDto<object>.Fail("Dirección no encontrada."));

        _context.Addresses.Remove(address);
        await _context.SaveChangesAsync();

        return Ok(ApiResponseDto<object>.Ok(null!, "Dirección eliminada correctamente."));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /api/customers/me/addresses/suggestions
    // Sugerencias de direcciones basadas en historial de pedidos (cliente logueado)
    // ──────────────────────────────────────────────────────────────────────────
    [HttpGet("me/addresses/suggestions")]
    public async Task<IActionResult> SugerenciasDirecciones()
    {
        var customerId = await ObtenerCustomerIdAsync();
        if (customerId == null)
            return NotFound(ApiResponseDto<object>.Fail("No tienes un perfil de cliente asociado."));

        // Sugerencias: direcciones guardadas + direcciones únicas de pedidos anteriores
        var guardadas = await _context.Addresses
            .Where(a => a.CustomerId == customerId.Value)
            .Select(a => new AddressSuggestionDto
            {
                Origen      = "guardada",
                Etiqueta    = a.Etiqueta,
                Calle       = a.Calle,
                Colonia     = a.Colonia,
                Municipio   = a.Municipio,
                Estado      = a.Estado,
                Cp          = a.Cp,
                Referencias = a.Referencias
            })
            .ToListAsync();

        var dePedidos = await _context.Orders
            .Where(o => o.CustomerId == customerId.Value)
            .OrderByDescending(o => o.FechaCreacion)
            .Take(10)
            .Select(o => new AddressSuggestionDto
            {
                Origen      = "historial",
                Etiqueta    = null,
                Calle       = o.DireccionEntregaCalle,
                Colonia     = o.DireccionEntregaColonia,
                Municipio   = o.DireccionEntregaMunicipio,
                Estado      = o.DireccionEntregaEstado,
                Cp          = o.DireccionEntregaCp,
                Referencias = o.DireccionEntregaReferencias
            })
            .ToListAsync();

        // Combinar y deduplicar por calle+colonia
        var todas = guardadas
            .Concat(dePedidos)
            .GroupBy(s => $"{s.Calle.ToLower()}|{s.Colonia.ToLower()}")
            .Select(g => g.First())
            .Take(10)
            .ToList();

        return Ok(ApiResponseDto<List<AddressSuggestionDto>>.Ok(todas));
    }

    // ── Helper privado ─────────────────────────────────────────────────────────
    private async Task<Guid?> ObtenerCustomerIdAsync()
    {
        var claim = User.FindFirst("sub")?.Value
                 ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(claim, out var userId)) return null;

        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
        return customer?.Id;
    }
}

// DTO adicional para sugerencias
public class AddressSuggestionDto
{
    public string   Origen      { get; set; } = string.Empty; // "guardada" | "historial"
    public string?  Etiqueta    { get; set; }
    public string   Calle       { get; set; } = string.Empty;
    public string   Colonia     { get; set; } = string.Empty;
    public string   Municipio   { get; set; } = string.Empty;
    public string   Estado      { get; set; } = string.Empty;
    public string?  Cp          { get; set; }
    public string?  Referencias { get; set; }
}
