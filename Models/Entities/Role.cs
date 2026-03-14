namespace FloreriaBautista.Models.Entities;

public class Role
{
    public Guid    Id          { get; set; }
    public string  Nombre      { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = [];
}
