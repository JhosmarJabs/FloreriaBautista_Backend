using Datadog.Trace;
using Datadog.Trace.Configuration;
using DotNetEnv;
using FloreriaBautista.Extensions;
using FloreriaBautista.Middleware;

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
    .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNameCaseInsensitive = true);

builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:3001", "http://localhost:5173")
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

app.UseCors("DefaultPolicy"); 
app.UseHttpsRedirection();
app.UseMiddleware<AlexaApiKeyMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
