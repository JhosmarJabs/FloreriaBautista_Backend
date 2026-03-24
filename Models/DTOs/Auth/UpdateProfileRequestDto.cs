namespace FloreriaBautista.Models.DTOs.Auth;

public class UpdateProfileRequestDto
{
    public string?  Nombre          { get; set; }
    public string?  ApellidoP       { get; set; }
    public string?  ApellidoM       { get; set; }
    public string?  Telefono        { get; set; }
    public string?  Sexo            { get; set; }
    public DateOnly? FechaNacimiento { get; set; }
}
