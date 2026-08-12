using System.Text;
using FloreriaBautista.Data;
using FloreriaBautista.Services.Backups;
using FloreriaBautista.Services.Database;
using FloreriaBautista.Services.Interfaces;
using FloreriaBautista.Services.Scheduler;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using FloreriaBautista.Services.Auth;
using FloreriaBautista.Services;
using FloreriaBautista.Services.Audit;
using FloreriaBautista.Services.ImportExport;
using FloreriaBautista.Services.Reports;
using FloreriaBautista.Services.Recommendations;
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

        // Caché en memoria (p. ej. la lista de reabastecimiento, que ejecuta el modelo por insumo)
        services.AddMemoryCache();

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

        // ── Fecha/hora del negocio ─────────────────────────────────
        // Singleton: la zona horaria se resuelve una sola vez. Cualquier "hoy" del
        // backend debe salir de aquí (ver IFechaHelper).
        services.AddSingleton<IFechaHelper, FechaHelper>();

        // ── Tareas programadas (background) ───────────────────────
        // Registrar como singleton para poder inyectarlo en el controller
        services.AddSingleton<BackupSchedulerService>();
        services.AddHostedService(sp => sp.GetRequiredService<BackupSchedulerService>());
        // El archivador de pedidos: la regla es scoped (la usan el scheduler y el
        // endpoint manual), el hosted service solo la dispara cada hora.
        services.AddScoped<IOrderArchiver, OrderArchiver>();
        services.AddHostedService<OrderArchiverService>();
        services.AddHostedService<PredictiveModelsSchedulerService>();

        // TODO: Registrar aquí los demás módulos
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAddressService, AddressService>();
        services.AddHttpClient<IMercadoPagoService, Services.Payments.MercadoPagoService>();
        services.AddScoped<IExportService, ExportService>();
        services.AddScoped<IImportService, ImportService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<ISupplyOrderService, SupplyOrderService>();
        services.AddScoped<ReportsService>();
        // Reportes por dominio (rediseño Agente 5): cada uno consulta su fuente real.
        services.AddScoped<Services.Reports.InventoryReportsService>();
        services.AddScoped<Services.Reports.SalesReportsService>();
        services.AddScoped<Services.Reports.PeopleReportsService>();
        services.AddScoped<CmsService>();

        // ── Modelos predictivos ─────────────────────────────────────
        // Solución 1 (regresión): la inferencia vive en el sidecar ml-service, que carga
        // el mismo modelo_surtido.pkl que produjo la libreta. Aquí solo se arma el cliente.
        services.AddHttpClient<IMlPredictionClient, Services.Analytics.MlPredictionClient>(client =>
        {
            var url = Environment.GetEnvironmentVariable("ML_SERVICE_URL") ?? "http://ml-service:8000";
            client.BaseAddress = new Uri(url);
            client.Timeout     = TimeSpan.FromSeconds(30);
        });

        // Solución 2 (recomendación): el artefacto es la matriz de similitud. Se carga una
        // sola vez en memoria como singleton; no hay inferencia que delegar.
        services.AddSingleton<Services.Recommendations.RecommenderArtifact>();
        services.AddScoped<IRecommendationService, RecommendationService>();
        services.AddScoped<ICustomerSegmentationService, Services.Analytics.CustomerSegmentationService>();

        return services;
    }

    private static string BuildConnectionString() =>
        $"Host={Env("DB_HOST")};" +
        $"Port={Env("DB_PORT")};" +
        $"Database={Env("DB_NAME")};" +
        $"Username={GetAdminUser()};" +
        $"Password={GetAdminPassword()};" +
        "Search Path=public;Include Error Detail=true";

    private static string GetAdminUser()
    {
        var adminUser = Environment.GetEnvironmentVariable("DB_ADMIN_USER");
        if (string.IsNullOrEmpty(adminUser) || adminUser == "app_user_admin")
        {
            return Env("DB_USER");
        }
        return adminUser;
    }

    private static string GetAdminPassword()
    {
        var adminPass = Environment.GetEnvironmentVariable("DB_ADMIN_PASSWORD");
        if (string.IsNullOrEmpty(adminPass) || adminPass == "Cambiar_antes_despliegue_Admin2025!")
        {
            return Env("DB_PASSWORD");
        }
        return adminPass;
    }

    private static string Env(string key) =>
        Environment.GetEnvironmentVariable(key)
        ?? throw new InvalidOperationException(
            $"Variable '{key}' no configurada. Verifica tu archivo .env");
}
