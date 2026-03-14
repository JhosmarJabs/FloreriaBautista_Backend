namespace FloreriaBautista.Models.DTOs.Auth;

public class LoginResponseDto
{
    public string       AccessToken  { get; set; } = string.Empty;
    public string       RefreshToken { get; set; } = string.Empty;
    public UsuarioDto   Usuario      { get; set; } = null!;
}

public class UsuarioDto
{
    public Guid         Id      { get; set; }
    public string       Nombre  { get; set; } = string.Empty;
    public string       Correo  { get; set; } = string.Empty;
    public List<string> Roles   { get; set; } = [];
}
