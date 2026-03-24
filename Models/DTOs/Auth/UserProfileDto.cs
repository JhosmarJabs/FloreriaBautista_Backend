namespace FloreriaBautista.Models.DTOs.Auth;

public class UserProfileDto
{
    public Guid         Id               { get; set; }
    public string       Nombre           { get; set; } = string.Empty;
    public string       ApellidoP        { get; set; } = string.Empty;
    public string?      ApellidoM        { get; set; }
    public string       Correo           { get; set; } = string.Empty;
    public string?      Telefono         { get; set; }
    public string?      Sexo             { get; set; }
    public DateOnly?    FechaNacimiento  { get; set; }
    public string       Estado           { get; set; } = string.Empty;
    public bool         CorreoVerificado { get; set; }
    public List<string> Roles            { get; set; } = [];
    public DateTime     CreadoEn         { get; set; }
}
