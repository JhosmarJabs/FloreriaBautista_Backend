using System.ComponentModel.DataAnnotations;

namespace FloreriaBautista.Models.DTOs.Auth;

public class LoginRequestDto
{
    [Required] [EmailAddress]
    public string Correo     { get; set; } = string.Empty;
    [Required]
    public string Contrasena { get; set; } = string.Empty;
}
