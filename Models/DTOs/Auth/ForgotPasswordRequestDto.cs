using System.ComponentModel.DataAnnotations;

namespace FloreriaBautista.Models.DTOs.Auth;

public class ForgotPasswordRequestDto
{
    [Required] [EmailAddress]
    public string Correo { get; set; } = string.Empty;
}
