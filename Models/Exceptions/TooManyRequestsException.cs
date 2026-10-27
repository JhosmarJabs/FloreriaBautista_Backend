namespace FloreriaBautista.Models.Exceptions;

/// <summary>
/// El usuario superó el límite de intentos permitido para una operación
/// (p. ej. códigos de cupón no válidos). El middleware la traduce a HTTP 429.
/// </summary>
public class TooManyRequestsException : AppException
{
    public TooManyRequestsException(string message) : base(message) { }
}
