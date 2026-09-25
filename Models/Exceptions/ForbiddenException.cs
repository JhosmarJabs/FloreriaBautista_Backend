namespace FloreriaBautista.Models.Exceptions;

/// <summary>
/// El usuario esta autenticado y tiene el rol correcto, pero no cumple una
/// condicion dinamica adicional (p. ej. no es el responsable de turno).
/// El middleware la traduce a HTTP 403 Forbidden.
/// </summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message = "No tiene permiso para esta accion.") : base(message) { }
}
