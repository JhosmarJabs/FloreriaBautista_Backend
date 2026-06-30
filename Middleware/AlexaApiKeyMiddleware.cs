using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FloreriaBautista.Middleware;

public class AlexaApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AlexaApiKeyMiddleware> _logger;
    private readonly string _expectedKey;
    private const string ALEXA_HEADER_NAME = "X-Alexa-API-Key";

    public AlexaApiKeyMiddleware(RequestDelegate next, ILogger<AlexaApiKeyMiddleware> logger, IConfiguration config)
    {
        _next        = next;
        _logger      = logger;
        _expectedKey = config["Alexa:ApiKey"]
            ?? throw new InvalidOperationException("La configuración 'Alexa:ApiKey' es requerida.");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;

        // Interceptar únicamente las peticiones dirigidas a la integración de Alexa
        if (path.StartsWithSegments("/api/alexa"))
        {
            if (!context.Request.Headers.TryGetValue(ALEXA_HEADER_NAME, out var extractedApiKey) ||
                extractedApiKey != _expectedKey)
            {
                _logger.LogWarning("Acceso denegado a {Path}: Cabecera {Header} inválida o ausente.", path, ALEXA_HEADER_NAME);
                
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"success\": false, \"message\": \"API Key de Alexa inválida o ausente.\"}");
                return;
            }

            _logger.LogInformation("API Key de Alexa validada exitosamente para la ruta {Path}.", path);

            // Inyectar un ClaimsPrincipal de Administrador en el contexto HTTP para bypassear la directiva [Authorize(Roles = "ADMIN")]
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "alexa-skill-client"),
                new Claim(ClaimTypes.Name, "Alexa Integration Client"),
                new Claim(ClaimTypes.Role, "ADMIN") // Rol ADMIN requerido por el controlador
            };

            var identity = new ClaimsIdentity(claims, "AlexaApiKeyAuthScheme");
            var principal = new ClaimsPrincipal(identity);
            context.User = principal;
        }

        await _next(context);
    }
}
