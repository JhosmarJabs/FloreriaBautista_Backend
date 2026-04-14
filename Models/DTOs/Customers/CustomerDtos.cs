namespace FloreriaBautista.Models.DTOs.Customers;

// ── Respuestas ─────────────────────────────────────────────────────────────────

public class CustomerSummaryDto
{
    public Guid     Id          { get; set; }
    public string   Nombre      { get; set; } = string.Empty;
    public string?  Apellido    { get; set; }
    public string   Telefono    { get; set; } = string.Empty;
    public string?  Correo      { get; set; }
    public string   TipoCliente { get; set; } = string.Empty;
    public int      TotalPedidos { get; set; }
    public DateTime CreadoEn   { get; set; }
}

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
    public DateTime CreadoEn  { get; set; }
}

// ── Requests ───────────────────────────────────────────────────────────────────

public class CreatePhysicalCustomerRequestDto
{
    public string   Nombre  { get; set; } = string.Empty;
    public string?  Apellido { get; set; }
    public string   Telefono { get; set; } = string.Empty;
    public string?  Correo  { get; set; }
}

public class SaveAddressRequestDto
{
    public string? Etiqueta    { get; set; }
    public string  Calle       { get; set; } = string.Empty;
    public string  Colonia     { get; set; } = string.Empty;
    public string  Municipio   { get; set; } = string.Empty;
    public string  Estado      { get; set; } = string.Empty;
    public string? Cp          { get; set; }
    public string? Referencias { get; set; }
}
