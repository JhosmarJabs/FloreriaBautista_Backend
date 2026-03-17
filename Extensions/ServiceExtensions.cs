using System.Text;
using FloreriaBautista.Data;
using FloreriaBautista.Services.Backups;
using FloreriaBautista.Services.Database;
using FloreriaBautista.Services.Interfaces;
using FloreriaBautista.Services.Scheduler;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using FloreriaBautista.Services.Auth;
using FloreriaBautista.Services;
using FloreriaBautista.Services.ImportExport;
using FloreriaBautista.Services.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace FloreriaBautista.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services, IConfiguration config)
    {
        // ── Base de datos ──────────────────────────────────────────
        // AppDbContext base — usado por servicios sin contexto HTTP (scheduler, etc.)
        services.AddDbContext<AppDbContext>(opt =>
            opt.UseNpgsql(BuildConnectionString())
               .UseSnakeCaseNamingConvention());

        // Factory que selecciona la conexión según el rol del usuario (ADMIN vs app_user)
        services.AddHttpContextAccessor();
        services.AddScoped<AppDbContextFactory>();

        // AppDbContext resuelto por rol — usado por controllers y servicios scoped
        services.AddScoped<AppDbContext>(sp =>
        {
            var factory = sp.GetRequiredService<AppDbContextFactory>();
            return factory.Crear();
        });

        // ── JWT ────────────────────────────────────────────────────
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opt =>
            {
                opt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer           = true,
                    ValidateAudience         = true,
                    ValidateLifetime         = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer              = Env("JWT_ISSUER"),
                    ValidAudience            = Env("JWT_AUDIENCE"),
                    IssuerSigningKey         = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(Env("JWT_KEY")))
                };
            });

        services.AddAuthorization();

        services.AddCors(opt =>
            opt.AddPolicy("AllowFrontend", p =>
                p.WithOrigins("http://localhost:5173", "https://floreriabautista.com")
                 .AllowAnyHeader()
                 .AllowAnyMethod()));

        // ── Servicios de backups ───────────────────────────────────
        services.AddScoped<GoogleDriveService>();
        services.AddScoped<IBackupService, BackupService>();

        // ── Servicios de base de datos ─────────────────────────────
        services.AddScoped<IDatabaseHealthService,      DatabaseHealthService>();
        services.AddScoped<IDatabaseMonitorService,     DatabaseMonitorService>();
        services.AddScoped<IDatabaseMaintenanceService, DatabaseMaintenanceService>();
        services.AddScoped<IRestoreService,             RestoreService>();

        // ── Tareas programadas (background) ───────────────────────
        services.AddHostedService<BackupSchedulerService>();

        // TODO: Registrar aquí los demás módulos
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IExportService, ExportService>();
        services.AddScoped<IImportService, ImportService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<ReportsService>();

        return services;
    }

    private static string BuildConnectionString() =>
        $"Host={Env("DB_HOST")};" +
        $"Port={Env("DB_PORT")};" +
        $"Database={Env("DB_NAME")};" +
        $"Username={Env("DB_USER")};" +
        $"Password={Env("DB_PASSWORD")}" +
        "Search Path=public;Include Error Detail=true";

    private static string Env(string key) =>
        Environment.GetEnvironmentVariable(key)
        ?? throw new InvalidOperationException(
            $"Variable '{key}' no configurada. Verifica tu archivo .env");
}
