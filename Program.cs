using Datadog.Trace;
using Datadog.Trace.Configuration;
using DotNetEnv;
using FloreriaBautista.Extensions;
using FloreriaBautista.Hubs;
using FloreriaBautista.Json;
using FloreriaBautista.Middleware;
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

builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddSwaggerDocumentation();

var app = builder.Build();

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
app.UseMiddleware<AlexaApiKeyMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<VentaInstantaneaHub>("/hubs/venta-instantanea");

app.Run();
