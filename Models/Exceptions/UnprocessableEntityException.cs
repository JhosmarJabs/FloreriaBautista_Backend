namespace FloreriaBautista.Models.Exceptions;

/// <summary>
/// La peticion es valida en forma, pero los datos del recurso impiden
/// procesarla (p. ej. un producto de venta instantanea sin exactamente una
/// flor primaria en su receta). El middleware la traduce a HTTP 422.
/// </summary>
public class UnprocessableEntityException : Exception
{
    public UnprocessableEntityException(string message) : base(message) { }
}
