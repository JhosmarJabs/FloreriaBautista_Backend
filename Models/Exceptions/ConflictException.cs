namespace FloreriaBautista.Models.Exceptions;

/// <summary>
/// La operacion no puede completarse porque el estado del recurso cambio
/// desde la ultima lectura (lock optimista, decision ya tomada, etc.).
/// El middleware la traduce a HTTP 409 Conflict.
/// </summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
