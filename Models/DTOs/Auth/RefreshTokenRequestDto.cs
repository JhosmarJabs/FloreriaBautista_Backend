using System.ComponentModel.DataAnnotations;

namespace FloreriaBautista.Models.DTOs.Auth;

public class RefreshTokenRequestDto
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
