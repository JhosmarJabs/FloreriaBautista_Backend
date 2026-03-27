using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FloreriaBautista.Middleware;

/// <summary>
/// Runtime Application Self-Protection (RASP) middleware.
/// Inspects every inbound HTTP request before it reaches controllers and blocks
/// requests that match known SQL-injection or XSS attack patterns.
///
/// Pipeline position:
///   ExceptionMiddleware → RequestLoggingMiddleware → RaspMiddleware → (auth, controllers…)
///
/// • After ExceptionMiddleware  → any unhandled error inside RASP is still caught.
/// • After RequestLoggingMiddleware → the logger records the request AND the 403 response,
///   giving a complete audit trail even for blocked requests.
/// • Before Authentication/Authorization → threats are killed before any DB or business-
///   logic code runs, which is the key RASP benefit.
/// </summary>
public class RaspMiddleware
{
    private readonly RequestDelegate          _next;
    private readonly ILogger<RaspMiddleware>  _logger;

    // ─── SQL-Injection patterns ───────────────────────────────────────────────
    // Each regex targets a distinct attack family. All are pre-compiled.
    // Tradeoff note: patterns are intentionally specific to minimise false positives
    // on legitimate business data (product descriptions, order notes, etc.).
    private static readonly Regex[] SqliPatterns =
    [
        // UNION-based extraction  →  "UNION SELECT …"
        new(@"\bunion\b.{0,60}\bselect\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // Inline comments used to evade filters  →  "1--" or "/*bypass*/"
        new(@"(--\s|\/\*[\s\S]*?\*\/)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // Boolean-blind  →  "OR 1=1"  /  "AND 1=0"
        new(@"\b(or|and)\b\s+\d+\s*=\s*\d+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // Stacked / destructive statements  →  "; DROP TABLE users"
        new(@";\s*(drop|alter|insert|update|delete|create|truncate)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // Time-based blind  →  "SLEEP(5)"  /  "WAITFOR DELAY"  /  "BENCHMARK(…)"
        new(@"\b(sleep|benchmark|waitfor\s+delay)\s*\(",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // Command execution (SQL Server)  →  "xp_cmdshell"  /  "EXEC("
        new(@"\b(xp_cmdshell|exec\s*\(|execute\s*\()",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // Hex / CHAR encoding used to obfuscate payloads  →  0x41414141  /  CHAR(65)
        new(@"(0x[0-9a-fA-F]{6,}|\bchar\s*\(\s*\d)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];

    // ─── XSS patterns ────────────────────────────────────────────────────────
    private static readonly Regex[] XssPatterns =
    [
        // Classic <script> injection
        new(@"<\s*script[\s>]",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // javascript: / vbscript: URI schemes
        new(@"(javascript|vbscript)\s*:",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // Inline event handlers  →  onclick=  onerror=  onload=  …
        // Matches common DOM events; avoids matching words like "ongoing"
        new(@"\bon(click|load|error|focus|blur|change|submit|mouse\w*|key\w*|drag\w*|scroll|input)\s*=",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // data:text/html URIs (can embed executable HTML)
        new(@"data\s*:\s*text\s*/\s*html",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // eval() / expression()  →  JS execution sinks
        new(@"\b(eval|expression)\s*\(",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];

    // ─── Paths exempt from RASP inspection ───────────────────────────────────
    // Add health-check probes, internal diagnostics, etc. here.
    private static readonly HashSet<string> ExcludedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health",
        "/favicon.ico",
    };

    // Upper limit for body reads to prevent memory exhaustion on large uploads.
    // Increase if your API legitimately receives very large JSON payloads.
    private const int MaxBodyBytesToInspect = 64 * 1024; // 64 KB

    public RaspMiddleware(RequestDelegate next, ILogger<RaspMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var req  = ctx.Request;
        var path = req.Path.Value ?? string.Empty;

        // Skip excluded endpoints (health checks, static files, etc.)
        if (ExcludedPaths.Contains(path))
        {
            await _next(ctx);
            return;
        }

        var ip     = ctx.Connection.RemoteIpAddress?.ToString() ?? "?";
        var method = req.Method;
        var url    = $"{req.Path}{req.QueryString}";

        // ── 1. Inspect QueryString ────────────────────────────────────────────
        if (req.QueryString.HasValue)
        {
            // URL-decode before scanning so encoded payloads (%27, %3D, etc.) are caught
            var query = Uri.UnescapeDataString(req.QueryString.Value!);

            var (isBlocked, pattern) = IsThreateningInput(query);
            if (isBlocked)
            {
                await BlockRequestAsync(ctx, ip, method, url, pattern, "QueryString");
                return;
            }
        }

        // ── 2. Inspect request body (JSON only — skip multipart/file uploads) ───
        // File uploads (multipart/form-data) are processed by a dedicated parser,
        // not interpolated into SQL, so scanning their raw bytes produces false positives.
        var contentType = req.ContentType ?? string.Empty;
        var isFileUpload = contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase);

        if (!isFileUpload && (req.ContentLength is > 0 || req.Headers.ContainsKey("Transfer-Encoding")))
        {
            // EnableBuffering replaces the one-shot stream with a seekable MemoryStream
            // so that (a) we can read it here and (b) controllers can still read it later.
            req.EnableBuffering();

            var body = await ReadBodyAsync(req, MaxBodyBytesToInspect);

            if (!string.IsNullOrWhiteSpace(body))
            {
                var (isBlocked, pattern) = IsThreateningInput(body);
                if (isBlocked)
                {
                    await BlockRequestAsync(ctx, ip, method, url, pattern, "Body");
                    return;
                }
            }

            // Rewind so downstream middleware / model binding can read the body normally
            req.Body.Position = 0;
        }

        await _next(ctx);
    }

    // ─── Detection engine ────────────────────────────────────────────────────

    /// <summary>
    /// Runs all SQLi and XSS regexes against <paramref name="input"/>.
    /// Returns on the first match to keep latency low.
    /// </summary>
    private static (bool matched, string patternLabel) IsThreateningInput(string input)
    {
        foreach (var regex in SqliPatterns)
        {
            var m = regex.Match(input);
            if (m.Success)
                return (true, $"SQLi | regex={regex} | matched={Truncate(m.Value, 60)}");
        }

        foreach (var regex in XssPatterns)
        {
            var m = regex.Match(input);
            if (m.Success)
                return (true, $"XSS  | regex={regex} | matched={Truncate(m.Value, 60)}");
        }

        return (false, string.Empty);
    }

    // ─── Blocking response ────────────────────────────────────────────────────

    private async Task BlockRequestAsync(
        HttpContext ctx,
        string ip, string method, string url,
        string patternLabel, string source)
    {
        // Log with enough detail to investigate the attempt without leaking the
        // full payload into application logs (only the matched excerpt is captured).
        _logger.LogWarning(
            "RASP BLOCKED | IP: {IP} | {Method} {Url} | Source: {Source} | {Pattern}",
            ip, method, url, source, patternLabel);

        // Expose block details to DatadogTracingMiddleware via HttpContext.Items
        // so they appear as tags on the APM span (security.rasp_blocked = true, etc.)
        ctx.Items["rasp.blocked"]      = true;
        ctx.Items["rasp.block_source"] = source;
        ctx.Items["rasp.block_pattern"] = patternLabel;

        ctx.Response.StatusCode  = StatusCodes.Status403Forbidden;
        ctx.Response.ContentType = "application/json";

        await ctx.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status  = 403,
            message = "Request blocked by RASP."
        }));
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads up to <paramref name="maxBytes"/> bytes from the request body.
    /// Uses <c>leaveOpen: true</c> so the underlying stream is NOT closed after reading.
    /// </summary>
    private static async Task<string> ReadBodyAsync(HttpRequest req, int maxBytes)
    {
        // Read only up to maxBytes to guard against huge payloads
        var buffer = new byte[maxBytes];
        var read   = await req.Body.ReadAsync(buffer.AsMemory(0, maxBytes));
        return Encoding.UTF8.GetString(buffer, 0, read);
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength] + "…";
}
