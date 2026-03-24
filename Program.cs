using DotNetEnv;
using FloreriaBautista.Extensions;
using FloreriaBautista.Middleware;

// Carga el archivo .env si existe (Development).
// En producción las variables vienen del sistema operativo o Docker.
Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Inyectar variables de entorno en la configuración de ASP.NET Core
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNameCaseInsensitive = true);
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddSwaggerDocumentation();

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

if (app.Environment.IsDevelopment())
    app.UseSwaggerDocumentation();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
