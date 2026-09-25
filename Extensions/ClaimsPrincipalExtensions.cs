using System.Security.Claims;
using FloreriaBautista.Models.Exceptions;

namespace FloreriaBautista.Extensions;

/// <summary>
/// Acceso al usuario autenticado desde un controller. Existe porque el id viaja
/// en <c>sub</c> (JwtRegisteredClaimNames.Sub) y ASP.NET a veces lo reexpone como
/// <see cref="ClaimTypes.NameIdentifier"/> según cómo esté configurado el mapeo de
/// claims; leer solo uno de los dos funciona hasta que deja de funcionar.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    public static Guid? UsuarioId(this ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst("sub")?.Value
                 ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    /// <summary>
    /// Id del usuario o excepción. Para endpoints que filtran por dueño: si el
    /// token no trae un id usable no se puede construir el filtro, y dejar pasar
    /// la consulta sin filtro sería exactamente la fuga que se quiere evitar.
    /// </summary>
    public static Guid UsuarioIdRequerido(this ClaimsPrincipal principal)
        => principal.UsuarioId()
           ?? throw new UnauthorizedException("El token no identifica a un usuario válido.");

    public static bool EsAdmin(this ClaimsPrincipal principal)
        => principal.IsInRole("ADMIN");
}
