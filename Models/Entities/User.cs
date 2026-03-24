namespace FloreriaBautista.Models.Entities;

public class User
{
    public Guid     Id                { get; set; }
    public string   Nombre            { get; set; } = string.Empty;
    public string   ApellidoP         { get; set; } = string.Empty;
    public string?  ApellidoM         { get; set; }
    public string   Correo            { get; set; } = string.Empty;
    public string?  Telefono          { get; set; }
    public string?  Sexo              { get; set; }
    public DateOnly? FechaNacimiento  { get; set; }
    public string?  PasswordHash      { get; set; }
    public bool     EsCliente         { get; set; } = false;
    public string   Estado            { get; set; } = "ACTIVO";
    public bool     CorreoVerificado  { get; set; } = false;
    public DateTime CreadoEn         { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadoEn    { get; set; } = DateTime.UtcNow;

    // Navegación
    public ICollection<UserRole>   UserRoles   { get; set; } = [];
    public ICollection<AuthToken>  AuthTokens  { get; set; } = [];
    public Customer?               Customer    { get; set; }
}
