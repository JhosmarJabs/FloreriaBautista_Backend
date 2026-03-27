using Datadog.Trace;
using Microsoft.AspNetCore.Routing;

namespace FloreriaBautista.Middleware;

/// <summary>
/// Wraps every HTTP request in a Datadog APM span, giving you distributed
/// tracing, latency histograms and error tracking in the Datadog UI.
///
/// Pipeline position: FIRST — before ExceptionMiddleware and all others.
/// Placing it first means the span covers the entire request lifecycle,
/// including error handling and RASP inspection.
///
/// Variable de entorno requeridas (en .env o Docker):
///   DD_SERVICE    → nombre del servicio en APM  (ej: "floreriabackend")
///   DD_ENV        → entorno                     (ej: "development", "production")
///   DD_VERSION    → versión del binario          (ej: "1.0.9")
///   DD_AGENT_HOST → host del Datadog Agent       (default: localhost)
///   DD_AGENT_PORT → puerto del Agent             (default: 8126)
/// </summary>
public class DatadogTracingMiddleware
{
    private readonly RequestDelegate _next;

    public DatadogTracingMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx)
    {
        var req    = ctx.Request;
        var method = req.Method;
        var path   = req.Path.Value ?? "/";

        // StartActive creates a span and makes it the "active" span for this
        // async context — child spans (DB queries via EF Core, etc.) will
        // automatically nest under it when using automatic instrumentation.
        using var scope = Tracer.Instance.StartActive("http.request");
        var span = scope.Span;

        // SpanTypes.Web tells Datadog this is a web server span
        span.Type         = SpanTypes.Web;
        // ResourceName groups traces in the APM Service map — use "METHOD /path"
        span.ResourceName = $"{method} {path}";

        // Standard HTTP semantic tags understood by Datadog APM
        span.SetTag(Tags.HttpMethod, method);
        span.SetTag(Tags.HttpUrl,    $"{req.Scheme}://{req.Host}{path}{req.QueryString}");
        span.SetTag("network.client.ip", ctx.Connection.RemoteIpAddress?.ToString() ?? "?");

        try
        {
            await _next(ctx);
        }
        catch (Exception ex)
        {
            // Only reaches here if ExceptionMiddleware itself throws (very rare).
            // Records exception details as span tags and re-throws so the framework
            // can still return an appropriate response.
            span.Error = true;
            span.SetException(ex);
            throw;
        }
        finally
        {
            // Response status is available here regardless of the execution path
            // (normal completion, RASP block, or exception turned into 5xx by ExceptionMiddleware).
            var status = ctx.Response.StatusCode;
            span.SetTag(Tags.HttpStatusCode, status.ToString());

            if (status >= 500)
                span.Error = true;

            // ── Route template ────────────────────────────────────────────────
            // Use "/api/orders/{id}" instead of "/api/orders/abc-123" so APM
            // groups all variants of the same endpoint together.
            if (ctx.GetEndpoint() is RouteEndpoint endpoint)
                span.SetTag("http.route", endpoint.RoutePattern.RawText ?? path);

            // ── RASP integration ──────────────────────────────────────────────
            // RaspMiddleware sets these Items when it blocks a request.
            // This surfaces security events directly in the APM trace.
            if (ctx.Items.TryGetValue("rasp.blocked", out var blocked) && blocked is true)
            {
                span.SetTag("security.rasp_blocked",  "true");
                span.SetTag("security.block_source",
                    ctx.Items.TryGetValue("rasp.block_source", out var src)
                        ? src?.ToString() ?? "unknown"
                        : "unknown");
                span.SetTag("security.block_pattern",
                    ctx.Items.TryGetValue("rasp.block_pattern", out var pat)
                        ? pat?.ToString() ?? ""
                        : "");
            }
        }
    }
}
