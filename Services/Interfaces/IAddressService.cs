using FloreriaBautista.Models.DTOs.Users;

namespace FloreriaBautista.Services.Interfaces;

public interface IAddressService
{
    Task<List<AddressDto>> ListarMisDireccionesAsync(Guid userId);
    Task<AddressDto>       CrearAsync(Guid userId, CreateAddressRequestDto request);
    Task<AddressDto>       ActualizarAsync(Guid userId, Guid addressId, UpdateAddressRequestDto request);
    Task                   EliminarAsync(Guid userId, Guid addressId);
    Task<AddressDto>       MarcarPrincipalAsync(Guid userId, Guid addressId);
}
