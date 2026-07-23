namespace FloreriaBautista.Models.DTOs.Users;

// Dirección de entrega del cliente autenticado.
public class AddressDto
{
    public Guid    Id          { get; set; }
    public string? Etiqueta    { get; set; }
    public string  Calle       { get; set; } = string.Empty;
    public string  Colonia     { get; set; } = string.Empty;
    public string  Municipio   { get; set; } = string.Empty;
    public string  Estado      { get; set; } = string.Empty;
    public string? Cp          { get; set; }
    public string? Referencias { get; set; }
    public bool    EsPrincipal { get; set; }
}

public class CreateAddressRequestDto
{
    public string? Etiqueta    { get; set; }
    public string  Calle       { get; set; } = string.Empty;
    public string  Colonia     { get; set; } = string.Empty;
    public string  Municipio   { get; set; } = string.Empty;
    public string  Estado      { get; set; } = string.Empty;
    public string? Cp          { get; set; }
    public string? Referencias { get; set; }
    public bool    EsPrincipal { get; set; } = false;
}

public class UpdateAddressRequestDto
{
    public string? Etiqueta    { get; set; }
    public string  Calle       { get; set; } = string.Empty;
    public string  Colonia     { get; set; } = string.Empty;
    public string  Municipio   { get; set; } = string.Empty;
    public string  Estado      { get; set; } = string.Empty;
    public string? Cp          { get; set; }
    public string? Referencias { get; set; }
    public bool    EsPrincipal { get; set; } = false;
}
