namespace FloreriaBautista.Models.Exceptions;
public class NotFoundException : AppException
{
    public NotFoundException(string entidad, object id)
        : base($"{entidad} con id '{id}' no encontrado.") { }
}
