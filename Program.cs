using Datadog.Trace;
using Datadog.Trace.Configuration;
using DotNetEnv;
using FloreriaBautista.Extensions;
using FloreriaBautista.Hubs;
using FloreriaBautista.Json;
using FloreriaBautista.Middleware;
using FloreriaBautista.PlayReview;
using Microsoft.Net.Http.Headers;

// Carga el archivo .env según el entorno.
var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

if (environment == "Emergency")
{
    Env.Load(".env.emergency");
}
else
{
    Env.TraversePath().Load();
}

var tracerSettings = TracerSettings.FromDefaultSources();
Tracer.Configure(tracerSettings);
var tracer = Tracer.Instance;   

var builder = WebApplication.CreateBuilder(args);

// Inyectar variables de entorno en la configuración de ASP.NET Core
builder.Configuration.AddEnvironmentVariables();

// appsettings.json usa placeholders tipo "${VAR}" (sintaxis Docker/Node) que .NET
// NO expande automáticamente. Aquí se mapea explícitamente la variable de entorno
// ALEXA_API_KEY (guion simple, como está configurada en Render) a la clave anidada
// "Alexa:ApiKey" que espera AlexaApiKeyMiddleware.
var alexaApiKeyEnv = Environment.GetEnvironmentVariable("ALEXA_API_KEY");
if (!string.IsNullOrWhiteSpace(alexaApiKeyEnv))
{
    builder.Configuration["Alexa:ApiKey"] = alexaApiKeyEnv;
}

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        // Emitir todos los DateTime como UTC con sufijo 'Z' para que el frontend
        // los convierta correctamente a hora local (ver UtcDateTimeConverter).
        o.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
        o.JsonSerializerOptions.Converters.Add(new NullableUtcDateTimeConverter());
    });

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// play_review: decide el papel de este proceso (borde en PROD, modo revisión en REVIEW
// o ninguno). Va antes de AddApplicationServices porque REVIEW cambia registros ahí.
// Una instancia de revisión mal configurada lanza aquí y no arranca. Ver docs/play-review.md.
var (bordeRevision, modoRevision) = builder.Services.AddPlayReview(builder.Environment);

builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddSwaggerDocumentation();

var app = builder.Build();

if (modoRevision.Activo)
{
    // REVIEW: primero la firma del borde (nadie entra directo), luego la auditoría
    // [play_review] y el bloqueo de operaciones de infraestructura.
    app.Logger.LogWarning("[play_review] Instancia de REVISIÓN activa: firma de borde obligatoria, " +
        "WhatsApp, push de Expo, respaldos y Mercado Pago apagados.");
    app.UseMiddleware<PlayReviewFirmaBordeMiddleware>();
    app.UseMiddleware<PlayReviewAuditoriaMiddleware>();
    app.UseMiddleware<PlayReviewBloqueoMiddleware>();
}
else if (bordeRevision.Activo)
{
    // PROD: el enrutador de borde va ANTES que todo (incluido Datadog y RequestLogging),
    // para que una petición de revisión no deje en PROD ni traza con su URL, ni log, ni
    // toque la autenticación o la base: se reenvía a REVIEW y termina aquí.
    app.Logger.LogInformation("[play_review-borde] Enrutador de borde activo hacia {Upstream}", bordeRevision.UpstreamUrl);
    app.UseMiddleware<PlayReviewEdgeRouter>();
}
else if (PlayReviewServiceExtensions.HayVariablesDeBorde())
{
    // Variables a medias o inválidas: PROD sigue normal y se avisa por qué.
    app.Logger.LogError("[play_review-borde] Enrutador de borde INACTIVO: {Motivo}", bordeRevision.MotivoInactivo);
}

app.UseMiddleware<DatadogTracingMiddleware>(); 
app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<RaspMiddleware>();          

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerDocumentation();
}

// CORS solo añade "Vary: Origin" con 2+ orígenes permitidos y nunca en rechazos; aquí se
// garantiza siempre (una sola vez) para que ninguna caché mezcle respuestas de orígenes distintos.
app.Use((context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var vary = context.Response.Headers.Vary;
        var yaIncluyeOrigin = vary
            .SelectMany(v => (v ?? string.Empty).Split(','))
            .Any(v => string.Equals(v.Trim(), HeaderNames.Origin, StringComparison.OrdinalIgnoreCase));
        if (!yaIncluyeOrigin)
        {
            context.Response.Headers.Append(HeaderNames.Vary, HeaderNames.Origin);
        }
        return Task.CompletedTask;
    });
    return next(context);
});

app.UseCors("DefaultPolicy");
app.UseHttpsRedirection();
// Alexa no existe en revisión: su middleware crea un ADMIN sintético con una API key
// y en REVIEW esa variable está prohibida (AlexaController además está bloqueado).
if (!modoRevision.Activo)
{
    app.UseMiddleware<AlexaApiKeyMiddleware>();
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<VentaInstantaneaHub>("/hubs/venta-instantanea");

app.Run();
