using System.Net;
using System.Text.Json;
using FloreriaBautista.Models.Exceptions;

namespace FloreriaBautista.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IWebHostEnvironment _env;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IWebHostEnvironment env)
    {
        _next   = next;
        _logger = logger;
        _env    = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try { await _next(context); }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            await HandleExceptionAsync(context, ex, _env.IsDevelopment());
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception ex, bool isDev)
    {
        var (code, msg) = ex switch
        {
            NotFoundException     => (HttpStatusCode.NotFound,           ex.Message),
            UnauthorizedException => (HttpStatusCode.Unauthorized,       ex.Message),
            AppException          => (HttpStatusCode.BadRequest,         ex.Message),
            _ => (HttpStatusCode.InternalServerError,
                  isDev
                    ? $"{ex.GetType().Name}: {ex.Message}" +
                      (ex.InnerException != null ? $" | Inner: {ex.InnerException.Message}" : "")
                    : "Error interno del servidor.")
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode  = (int)code;

        return context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status  = (int)code,
            message = msg
        }));
    }
}
