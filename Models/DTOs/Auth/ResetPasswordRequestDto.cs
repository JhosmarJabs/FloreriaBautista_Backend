using System.ComponentModel.DataAnnotations;

namespace FloreriaBautista.Models.DTOs.Auth;

public class ResetPasswordRequestDto
{
    [Required] public string Token       { get; set; } = string.Empty;
    [Required] [MinLength(6)]
    public string NuevaContrasena        { get; set; } = string.Empty;
}
