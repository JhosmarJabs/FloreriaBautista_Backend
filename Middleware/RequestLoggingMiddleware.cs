namespace FloreriaBautista.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate              _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var req    = ctx.Request;
        var origin = req.Headers["Origin"].FirstOrDefault()
                  ?? req.Headers["Referer"].FirstOrDefault()
                  ?? req.Headers["User-Agent"].FirstOrDefault()
                  ?? "Desconocido";

        var ip     = ctx.Connection.RemoteIpAddress?.ToString() ?? "?";
        var sw     = System.Diagnostics.Stopwatch.StartNew();

        _logger.LogInformation(
            "→ [{Method}] {Path}{Query} | Origen: {Origin} | IP: {IP}",
            req.Method,
            req.Path,
            req.QueryString,
            origin,
            ip);

        await _next(ctx);

        sw.Stop();
        var status = ctx.Response.StatusCode;
        var emoji  = status < 300 ? "✅" : status < 400 ? "↪️" : status < 500 ? "⚠️" : "❌";

        _logger.LogInformation(
            "{Emoji} [{Status}] {Method} {Path} | {Ms} ms | IP: {IP}",
            emoji,
            status,
            req.Method,
            req.Path,
            sw.ElapsedMilliseconds,
            ip);
    }
}
