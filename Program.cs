using Datadog.Trace;
using Datadog.Trace.Configuration;
using DotNetEnv;
using FloreriaBautista.Extensions;
using FloreriaBautista.Middleware;

// Carga el archivo .env si existe (Development).
// En producción las variables vienen del sistema operativo o Docker.
Env.TraversePath().Load();

// ── Datadog APM ────────────────────────────────────────────────────────────────
// TracerSettings.FromDefaultSources() lee las variables DD_* del entorno.
// Configura estas variables en tu .env (desarrollo) o en Docker/Kubernetes (producción):
//
//   DD_SERVICE    = floreriabackend
//   DD_ENV        = development | production
//   DD_VERSION    = 1.0.9
//   DD_AGENT_HOST = localhost          (o el hostname del contenedor del Agent)
//   DD_AGENT_PORT = 8126
//
// Sin un Agent corriendo el tracer opera en modo no-op (sin errores, sin envío de datos).
var tracerSettings = TracerSettings.FromDefaultSources();
Tracer.Configure(tracerSettings);
var tracer = Tracer.Instance;   // instancia global — úsala en cualquier parte de la app

var builder = WebApplication.CreateBuilder(args);

// Inyectar variables de entorno en la configuración de ASP.NET Core
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNameCaseInsensitive = true);
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddSwaggerDocumentation();

var app = builder.Build();

app.UseMiddleware<DatadogTracingMiddleware>(); // Datadog: primer middleware — cubre todo el ciclo de vida
app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<RaspMiddleware>();          // RASP: bloquea SQLi / XSS antes de llegar a los controladores

if (app.Environment.IsDevelopment())
    app.UseSwaggerDocumentation();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
