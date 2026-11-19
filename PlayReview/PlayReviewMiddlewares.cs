using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using FloreriaBautista.Models.DTOs.Common;

namespace FloreriaBautista.PlayReview;

/// <summary>
/// REVIEW: rechaza con 403 toda petición que no traiga una firma de borde válida y
/// vigente (±300 s). Así la URL de Render de la instancia de revisión no sirve de nada
/// llamada directamente desde internet. Única excepción: <c>GET/HEAD /api/ping</c>
/// para el keep-alive (no toca la base).
/// </summary>
public class PlayReviewFirmaBordeMiddleware
{
    private readonly RequestDelegate                         _next;
    private readonly OpcionesModoRevision                    _opciones;
    private readonly ILogger<PlayReviewFirmaBordeMiddleware> _logger;

    public PlayReviewFirmaBordeMiddleware(
        RequestDelegate next, OpcionesModoRevision opciones, ILogger<PlayReviewFirmaBordeMiddleware> logger)
    {
        _next     = next;
        _opciones = opciones;
        _logger   = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var req = ctx.Request;

        if ((HttpMethods.IsGet(req.Method) || HttpMethods.IsHead(req.Method))
            && string.Equals(req.Path.Value, PlayReviewEntorno.RutaPing, StringComparison.OrdinalIgnoreCase))
        {
            await _next(ctx);
            return;
        }

        var ruta      = (req.PathBase + req.Path).Value ?? "/";
        var resultado = FirmaBorde.Validar(
            _opciones.Secreto, req.Headers[PlayReviewEntorno.HeaderFirma].ToString(),
            req.Method, ruta, DateTimeOffset.UtcNow);

        if (resultado != ResultadoFirma.Valida)
        {
            _logger.LogWarning(
                "{Prefijo} rechazada sin firma de borde válida ({Resultado}) | {Method} {Path} | IP: {IP} | UA: {UA}",
                PlayReviewEntorno.PrefijoLog, resultado, req.Method, req.Path,
                PlayReviewAuditoriaMiddleware.IpCliente(ctx), req.Headers.UserAgent.ToString());

            await RespuestasRevision.EscribirAsync(ctx, StatusCodes.Status403Forbidden, PlayReviewEntorno.MensajeSinFirma);
            return;
        }

        // Ya cumplió su función; que nada aguas abajo lo vea.
        req.Headers.Remove(PlayReviewEntorno.HeaderFirma);
        await _next(ctx);
    }
}

/// <summary>
/// REVIEW: deja en el log una línea por petición con el prefijo <c>[play_review]</c>:
/// usuario, método, ruta (sin query: puede traer el access_token), estado, IP y user-agent.
/// El usuario se lee después de <c>next</c>, cuando la autenticación ya corrió.
/// </summary>
public class PlayReviewAuditoriaMiddleware
{
    private readonly RequestDelegate                        _next;
    private readonly ILogger<PlayReviewAuditoriaMiddleware> _logger;

    public PlayReviewAuditoriaMiddleware(RequestDelegate next, ILogger<PlayReviewAuditoriaMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        finally
        {
            var user    = ctx.User;
            var usuario = user?.Identity?.IsAuthenticated == true
                ? $"{user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value} " +
                  $"<{user.FindFirst(ClaimTypes.Email)?.Value ?? user.FindFirst("email")?.Value}>"
                : "anónimo";

            _logger.LogInformation(
                "{Prefijo} {Usuario} | {Method} {Path} → {Status} | IP: {IP} | UA: {UA}",
                PlayReviewEntorno.PrefijoLog, usuario, ctx.Request.Method, ctx.Request.Path,
                ctx.Response.StatusCode, IpCliente(ctx), ctx.Request.Headers.UserAgent.ToString());
        }
    }

    /// <summary>
    /// La IP real viene en el primer valor de X-Forwarded-For (lo arma el proxy de Render
    /// en PROD y YARP lo conserva al reenviar). Solo se usa para auditoría, nunca para autorizar.
    /// </summary>
    internal static string IpCliente(HttpContext ctx)
    {
        var xff = ctx.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(xff)) return xff.Split(',')[0].Trim();
        return ctx.Connection.RemoteIpAddress?.ToString() ?? "?";
    }
}

/// <summary>
/// Marca un controlador o una acción como prohibida en la instancia de revisión
/// (respaldos, restauración, mantenimiento, scheduler, WhatsApp, usuarios y roles,
/// importación/exportación, pagos reales, Alexa, herramientas de desarrollo).
/// En PROD no tiene ningún efecto.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public sealed class BloqueadoEnRevisionAttribute : Attribute { }

/// <summary>
/// REVIEW: responde 403 (mensaje en español e inglés) a cualquier endpoint marcado con
/// <see cref="BloqueadoEnRevisionAttribute"/>. Va antes de la autenticación, así que
/// tampoco revela si la ruta pediría login.
/// </summary>
public class PlayReviewBloqueoMiddleware
{
    private readonly RequestDelegate                      _next;
    private readonly ILogger<PlayReviewBloqueoMiddleware> _logger;

    public PlayReviewBloqueoMiddleware(RequestDelegate next, ILogger<PlayReviewBloqueoMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (ctx.GetEndpoint()?.Metadata.GetMetadata<BloqueadoEnRevisionAttribute>() is not null)
        {
            _logger.LogInformation("{Prefijo} operación bloqueada en revisión: {Method} {Path}",
                PlayReviewEntorno.PrefijoLog, ctx.Request.Method, ctx.Request.Path);
            await RespuestasRevision.EscribirAsync(ctx, StatusCodes.Status403Forbidden, PlayReviewEntorno.MensajeBloqueado);
            return;
        }

        await _next(ctx);
    }
}

internal static class RespuestasRevision
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        // Acentos legibles en el mensaje bilingüe (sigue escapando lo peligroso para HTML).
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public static Task EscribirAsync(HttpContext ctx, int status, string mensaje)
    {
        ctx.Response.StatusCode  = status;
        ctx.Response.ContentType = "application/json";
        return ctx.Response.WriteAsync(JsonSerializer.Serialize(ApiResponseDto<object>.Fail(mensaje), Json));
    }
}
