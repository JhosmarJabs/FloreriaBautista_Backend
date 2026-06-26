using System.Collections.Generic;

namespace FloreriaBautista.Models.DTOs.Users;

public class UpdateUserRequestDto
{
    public string?       Nombre   { get; set; }
    public string?       Apellido { get; set; }
    public string?       Telefono { get; set; }
    public string?       Sexo     { get; set; }
    public DateTime?     FechaNacimiento { get; set; }
    public bool?         Activo   { get; set; }
    public List<string>? Roles    { get; set; }
}
