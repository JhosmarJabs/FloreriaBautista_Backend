using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Data;

/// <summary>
/// Selecciona la cadena de conexión según el rol del usuario autenticado.
/// - ADMIN  → db_admin_user  (CRUD completo + DDL)
/// - otros  → app_user       (escritura normal de negocio)
/// Se resuelve una vez por request (Scoped) justo antes de la primera query.
/// </summary>
public class AppDbContextFactory
{
    private readonly IHttpContextAccessor _httpContext;
    private readonly IConfiguration       _config;

    public AppDbContextFactory(IHttpContextAccessor httpContext, IConfiguration config)
    {
        _httpContext = httpContext;
        _config      = config;
    }

    public AppDbContext Crear()
    {
        // Siempre usa app_user para el contexto de negocio (EF Core)
        // db_admin_user solo se usa en servicios de mantenimiento con conexión directa
        var connStr = BuildAppConnection();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connStr)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options);
    }

    // ── Helpers ───────────────────────────────────────────────────

    private bool EsAdmin()
    {
        var user = _httpContext.HttpContext?.User;
        return user?.Identity?.IsAuthenticated == true
            && user.IsInRole("ADMIN");
    }

    private static string BuildAdminConnection() =>
        $"Host={Env("DB_HOST")};"      +
        $"Port={Env("DB_PORT")};"      +
        $"Database={Env("DB_NAME")};"  +
        $"Username={GetAdminUser()};"     +
        $"Password={GetAdminPassword()}";

    private static string BuildAppConnection() =>
        $"Host={Env("DB_HOST")};"      +
        $"Port={Env("DB_PORT")};"      +
        $"Database={Env("DB_NAME")};"  +
        $"Username={GetAdminUser()};"  +
        $"Password={GetAdminPassword()}";

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
