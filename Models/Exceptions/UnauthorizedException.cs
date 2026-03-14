namespace FloreriaBautista.Models.Exceptions;
public class UnauthorizedException : AppException
{
    public UnauthorizedException(string message = "No autorizado.") : base(message) { }
}
