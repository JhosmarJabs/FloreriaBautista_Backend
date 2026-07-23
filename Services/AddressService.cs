using Microsoft.EntityFrameworkCore;
using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Users;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Services.Interfaces;

namespace FloreriaBautista.Services;

public class AddressService : IAddressService
{
    private readonly AppDbContext             _context;
    private readonly ILogger<AddressService>  _logger;

    public AddressService(AppDbContext context, ILogger<AddressService> logger)
    {
        _context = context;
        _logger  = logger;
    }

    // ── Listar direcciones del cliente autenticado ────────────────
    public async Task<List<AddressDto>> ListarMisDireccionesAsync(Guid userId)
    {
        var customer = await ObtenerClienteAsync(userId);
        if (customer == null) return [];

        var direcciones = await _context.Addresses
            .Where(a => a.CustomerId == customer.Id)
            .OrderByDescending(a => a.EsPrincipal)
            .ThenBy(a => a.CreadoEn)
            .ToListAsync();

        return direcciones.Select(MapToDto).ToList();
    }

    // ── Crear ─────────────────────────────────────────────────────
    public async Task<AddressDto> CrearAsync(Guid userId, CreateAddressRequestDto request)
    {
        Validar(request.Calle, request.Colonia, request.Municipio, request.Estado);
        var customer = await ObtenerOCrearClienteAsync(userId);

        var yaHayDirecciones = await _context.Addresses.AnyAsync(a => a.CustomerId == customer.Id);

        var address = new Address
        {
            Id          = Guid.NewGuid(),
            CustomerId  = customer.Id,
            Etiqueta    = request.Etiqueta?.Trim(),
            Calle       = request.Calle.Trim(),
            Colonia     = request.Colonia.Trim(),
            Municipio   = request.Municipio.Trim(),
            Estado      = request.Estado.Trim(),
            Cp          = request.Cp?.Trim(),
            Referencias = request.Referencias?.Trim(),
            // La primera dirección siempre es principal; después respeta lo pedido.
            EsPrincipal = !yaHayDirecciones || request.EsPrincipal,
            CreadoEn    = DateTime.UtcNow,
        };

        if (address.EsPrincipal)
            await QuitarPrincipalAOtrasAsync(customer.Id, null);

        _context.Addresses.Add(address);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Dirección creada {AddressId} para usuario {UserId}", address.Id, userId);
        return MapToDto(address);
    }

    // ── Actualizar ────────────────────────────────────────────────
    public async Task<AddressDto> ActualizarAsync(Guid userId, Guid addressId, UpdateAddressRequestDto request)
    {
        Validar(request.Calle, request.Colonia, request.Municipio, request.Estado);
        var (customer, address) = await ObtenerPropiaAsync(userId, addressId);

        address.Etiqueta    = request.Etiqueta?.Trim();
        address.Calle       = request.Calle.Trim();
        address.Colonia     = request.Colonia.Trim();
        address.Municipio   = request.Municipio.Trim();
        address.Estado      = request.Estado.Trim();
        address.Cp          = request.Cp?.Trim();
        address.Referencias = request.Referencias?.Trim();

        // No se permite "desmarcar" la principal directamente; solo promover otra.
        if (request.EsPrincipal && !address.EsPrincipal)
        {
            await QuitarPrincipalAOtrasAsync(customer.Id, address.Id);
            address.EsPrincipal = true;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Dirección actualizada {AddressId} para usuario {UserId}", addressId, userId);
        return MapToDto(address);
    }

    // ── Eliminar ──────────────────────────────────────────────────
    public async Task EliminarAsync(Guid userId, Guid addressId)
    {
        var (customer, address) = await ObtenerPropiaAsync(userId, addressId);
        var eraPrincipal = address.EsPrincipal;

        _context.Addresses.Remove(address);
        await _context.SaveChangesAsync();

        // Si se borró la principal, se promueve la más antigua que quede.
        if (eraPrincipal)
        {
            var siguiente = await _context.Addresses
                .Where(a => a.CustomerId == customer.Id)
                .OrderBy(a => a.CreadoEn)
                .FirstOrDefaultAsync();
            if (siguiente != null)
            {
                siguiente.EsPrincipal = true;
                await _context.SaveChangesAsync();
            }
        }

        _logger.LogInformation("Dirección eliminada {AddressId} para usuario {UserId}", addressId, userId);
    }

    // ── Marcar como principal ─────────────────────────────────────
    public async Task<AddressDto> MarcarPrincipalAsync(Guid userId, Guid addressId)
    {
        var (customer, address) = await ObtenerPropiaAsync(userId, addressId);

        if (!address.EsPrincipal)
        {
            await QuitarPrincipalAOtrasAsync(customer.Id, address.Id);
            address.EsPrincipal = true;
            await _context.SaveChangesAsync();
        }

        return MapToDto(address);
    }

    // ── Helpers ───────────────────────────────────────────────────
    private static void Validar(string calle, string colonia, string municipio, string estado)
    {
        if (string.IsNullOrWhiteSpace(calle))     throw new AppException("La calle es obligatoria.");
        if (string.IsNullOrWhiteSpace(colonia))   throw new AppException("La colonia es obligatoria.");
        if (string.IsNullOrWhiteSpace(municipio)) throw new AppException("El municipio es obligatorio.");
        if (string.IsNullOrWhiteSpace(estado))    throw new AppException("El estado es obligatorio.");
    }

    private async Task<Customer?> ObtenerClienteAsync(Guid userId) =>
        await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);

    // Devuelve el cliente ligado al usuario; si no existe (usuario web sin
    // pedidos aún), lo crea a partir de los datos del propio usuario.
    private async Task<Customer> ObtenerOCrearClienteAsync(Guid userId)
    {
        var customer = await ObtenerClienteAsync(userId);
        if (customer != null) return customer;

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new NotFoundException("Usuario", userId);

        customer = new Customer
        {
            Id          = Guid.NewGuid(),
            UserId      = user.Id,
            TipoCliente = "SESION",
            Nombre      = user.Nombre,
            Apellido    = user.Apellido,
            Telefono    = user.Telefono ?? "",
            Correo      = user.Correo,
            Sexo        = user.Sexo,
            CreadoEn    = DateTime.UtcNow,
        };
        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();
        return customer;
    }

    // Trae una dirección garantizando que pertenece al usuario autenticado.
    private async Task<(Customer customer, Address address)> ObtenerPropiaAsync(Guid userId, Guid addressId)
    {
        var customer = await ObtenerClienteAsync(userId)
            ?? throw new NotFoundException("Cliente", userId);

        var address = await _context.Addresses
            .FirstOrDefaultAsync(a => a.Id == addressId && a.CustomerId == customer.Id)
            ?? throw new NotFoundException("Dirección", addressId);

        return (customer, address);
    }

    private async Task QuitarPrincipalAOtrasAsync(Guid customerId, Guid? exceptoId)
    {
        var otras = await _context.Addresses
            .Where(a => a.CustomerId == customerId && a.EsPrincipal && (exceptoId == null || a.Id != exceptoId))
            .ToListAsync();
        foreach (var a in otras) a.EsPrincipal = false;
    }

    private static AddressDto MapToDto(Address a) => new()
    {
        Id          = a.Id,
        Etiqueta    = a.Etiqueta,
        Calle       = a.Calle,
        Colonia     = a.Colonia,
        Municipio   = a.Municipio,
        Estado      = a.Estado,
        Cp          = a.Cp,
        Referencias = a.Referencias,
        EsPrincipal = a.EsPrincipal,
    };
}
