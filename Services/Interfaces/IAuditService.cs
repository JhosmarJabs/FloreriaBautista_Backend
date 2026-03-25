namespace FloreriaBautista.Services.Interfaces;

public interface IAuditService
{
    Task RegistrarAsync(string accion, string entidad, string? entidadId, Guid? usuarioId, object? detalles = null);
}
