using System.ComponentModel.DataAnnotations;

namespace FloreriaBautista.Models.DTOs.Auth;

public class RegisterRequestDto
{
    [Required] public string Nombre   { get; set; } = string.Empty;
    [Required] public string Apellido { get; set; } = string.Empty;
    [Required] [EmailAddress] public string Correo     { get; set; } = string.Empty;
    [Required] [MinLength(6)] public string Contrasena { get; set; } = string.Empty;
    public string? Telefono { get; set; }
}
