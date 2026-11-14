using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Auth;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Services.Interfaces;

namespace FloreriaBautista.Services.Auth;

public class AuthService : IAuthService
{
    private readonly AppDbContext         _context;
    private readonly ILogger<AuthService> _logger;

    public AuthService(AppDbContext context, ILogger<AuthService> logger)
    {
        _context = context;
        _logger  = logger;
    }

    // ── Login ─────────────────────────────────────────────────────
    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Correo == request.Correo.ToLower().Trim());

        if (user == null)
            throw new UnauthorizedException("Correo o contraseña incorrectos.");

        if (user.Estado != "ACTIVO")
            throw new UnauthorizedException("Tu cuenta está desactivada.");

        if (!BCrypt.Net.BCrypt.Verify(request.Contrasena, user.PasswordHash))
            throw new UnauthorizedException("Correo o contraseña incorrectos.");

        var roles = user.UserRoles.Select(ur => ur.Role.Nombre).ToList();
        var accessToken  = GenerarAccessToken(user, roles);
        var refreshToken = (await GuardarRefreshTokenAsync(user.Id)).Token; // inicia una familia nueva

        _logger.LogInformation("Login exitoso: {Correo}", user.Correo);

        return BuildLoginResponse(user, roles, accessToken, refreshToken);
    }

    // ── Register ──────────────────────────────────────────────────
    public async Task<LoginResponseDto> RegisterAsync(RegisterRequestDto request)
    {
        var existe = await _context.Users
            .AnyAsync(u => u.Correo == request.Correo.ToLower().Trim());

        if (existe)
            throw new AppException("Ya existe una cuenta con ese correo.");

        var rolCliente = await _context.Roles
            .FirstOrDefaultAsync(r => r.Nombre == "CLIENTE")
            ?? throw new AppException("Rol CLIENTE no encontrado.");

        var user = new User
        {
            Id               = Guid.NewGuid(),
            Nombre           = request.Nombre.Trim(),
            Apellido         = request.Apellido.Trim(),
            Correo           = request.Correo.ToLower().Trim(),
            Telefono         = request.Telefono,
            PasswordHash     = BCrypt.Net.BCrypt.HashPassword(request.Contrasena),
            EsCliente        = true,
            Estado           = "ACTIVO",
            CorreoVerificado = false,
            CreadoEn         = DateTime.UtcNow,
            ActualizadoEn    = DateTime.UtcNow
        };

        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = rolCliente.Id });
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Nuevo cliente registrado: {Correo}", user.Correo);
        return await LoginAsync(new LoginRequestDto { Correo = request.Correo, Contrasena = request.Contrasena });
    }

    // ── Refresh ───────────────────────────────────────────────────
    // Renovación idempotente (plan sesion-renovacion-idempotente). La decisión la toma
    // PoliticaRenovacion; aquí solo se lee el estado, se rota dentro de una transacción
    // y se ejecutan las consecuencias. Todos los rechazos usan el mismo 401.
    private const string MensajeRefreshInvalido = "Refresh token inválido o expirado.";

    public async Task<LoginResponseDto> RefreshAsync(string refreshToken)
    {
        var ventana   = LeerVentanaReusoSegundos();
        var deteccion = LeerDeteccionReuso();

        // Máximo dos pasadas: la segunda solo ocurre si otra petición ganó el canje.
        for (var pasada = 0; pasada < 2; pasada++)
        {
            var ahora = DateTime.UtcNow;
            var t = await LeerParaRenovarAsync(refreshToken);
            var (decision, sucesor) = await DecidirAsync(t, ahora, ventana, deteccion);

            switch (decision)
            {
                case DecisionRenovacion.Rotar:
                    if (pasada == 1) throw new UnauthorizedException(MensajeRefreshInvalido);
                    var rotado = await RotarAsync(t!, ahora);
                    if (rotado != null) return rotado;
                    break; // otra petición ganó: reevaluar

                case DecisionRenovacion.Reintentar:
                {
                    var user  = t!.User;
                    var roles = user.UserRoles.Select(ur => ur.Role.Nombre).ToList();
                    _logger.LogInformation(
                        "Renovación idempotente: reintento dentro de la ventana. Usuario {UserId}, familia {FamiliaId}, {Segundos:0.0} s desde la rotación",
                        user.Id, t.FamiliaId ?? t.Id, SegundosDesde(t.CerradoEn, ahora));
                    return BuildLoginResponse(user, roles, GenerarAccessToken(user, roles), sucesor!.Token);
                }

                case DecisionRenovacion.ReusoDetectado:
                {
                    var familia = t!.FamiliaId ?? t.Id;
                    var revocadas = await _context.AuthTokens
                        .Where(x => x.UserId == t.UserId && x.Tipo == PoliticaRenovacion.TipoRefresh && !x.Usado &&
                                    (x.FamiliaId == familia || x.Id == familia))
                        .ExecuteUpdateAsync(x => x
                            .SetProperty(r => r.Usado, true)
                            .SetProperty(r => r.MotivoCierre, PoliticaRenovacion.ReusoDetectado)
                            .SetProperty(r => r.CerradoEn, (DateTime?)ahora));
                    _logger.LogWarning(
                        "Reutilización de refresh token detectada. Usuario {UserId}, familia {FamiliaId}, {Segundos:0.0} s desde la rotación, {Revocadas} token(s) revocados",
                        t.UserId, familia, SegundosDesde(t.CerradoEn, ahora), revocadas);
                    throw new UnauthorizedException(MensajeRefreshInvalido);
                }

                default: // Rechazar
                    // Cuenta desactivada: el token activo se cierra con su motivo (mismo 401 para el cliente).
                    if (t != null && t.Tipo == PoliticaRenovacion.TipoRefresh && !t.Usado &&
                        t.ExpiraEn > ahora && t.User.Estado != "ACTIVO")
                    {
                        await _context.AuthTokens
                            .Where(x => x.Id == t.Id && !x.Usado)
                            .ExecuteUpdateAsync(x => x
                                .SetProperty(r => r.Usado, true)
                                .SetProperty(r => r.MotivoCierre, PoliticaRenovacion.CuentaInactiva)
                                .SetProperty(r => r.CerradoEn, (DateTime?)ahora));
                    }
                    throw new UnauthorizedException(MensajeRefreshInvalido);
            }
        }

        throw new UnauthorizedException(MensajeRefreshInvalido);
    }

    /// <summary>Lee el token presentado sin seguimiento, con su usuario y roles.</summary>
    private Task<AuthToken?> LeerParaRenovarAsync(string refreshToken) =>
        _context.AuthTokens
            .AsNoTracking()
            .Include(t => t.User).ThenInclude(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(t => t.Token == refreshToken && t.Tipo == PoliticaRenovacion.TipoRefresh);

    private async Task<(DecisionRenovacion Decision, AuthToken? Sucesor)> DecidirAsync(
        AuthToken? t, DateTime ahora, int ventana, bool deteccion)
    {
        AuthToken? sucesor = null;
        if (t != null && t.Usado && t.SucesorId.HasValue)
        {
            sucesor = await _context.AuthTokens.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == t.SucesorId.Value);
        }

        var decision = PoliticaRenovacion.Decidir(
            Resumir(t), Resumir(sucesor), t?.User.Estado == "ACTIVO", ahora, ventana, deteccion);
        return (decision, sucesor);
    }

    private static TokenRenovacion? Resumir(AuthToken? t) =>
        t == null ? null : new TokenRenovacion(t.Tipo, t.UserId, t.ExpiraEn, t.Usado, t.MotivoCierre, t.CerradoEn);

    private static double SegundosDesde(DateTime? momento, DateTime ahora) =>
        momento.HasValue ? (ahora - momento.Value).TotalSeconds : -1;

    /// <summary>
    /// Crea el sucesor y reclama T en una sola transacción. Devuelve null si otra petición
    /// canjeó T primero (se revierte todo y el llamador reevalúa).
    /// </summary>
    private async Task<LoginResponseDto?> RotarAsync(AuthToken t, DateTime ahora)
    {
        await using var tx = await _context.Database.BeginTransactionAsync();

        var sucesor = await GuardarRefreshTokenAsync(t.UserId, t.FamiliaId ?? t.Id);

        var reclamadas = await _context.AuthTokens
            .Where(x => x.Id == t.Id && !x.Usado && x.ExpiraEn > ahora)
            .ExecuteUpdateAsync(x => x
                .SetProperty(r => r.Usado, true)
                .SetProperty(r => r.MotivoCierre, PoliticaRenovacion.Rotado)
                .SetProperty(r => r.CerradoEn, (DateTime?)ahora)
                .SetProperty(r => r.SucesorId, (Guid?)sucesor.Id));

        if (reclamadas == 1)
        {
            await tx.CommitAsync();
            var user  = t.User;
            var roles = user.UserRoles.Select(ur => ur.Role.Nombre).ToList();
            return BuildLoginResponse(user, roles, GenerarAccessToken(user, roles), sucesor.Token);
        }

        await tx.RollbackAsync();
        _context.Entry(sucesor).State = EntityState.Detached;
        return null;
    }

    // ── Logout ────────────────────────────────────────────────────
    public async Task LogoutAsync(string refreshToken)
    {
        var token = await _context.AuthTokens
            .FirstOrDefaultAsync(t => t.Token == refreshToken && t.Tipo == PoliticaRenovacion.TipoRefresh);
        if (token == null) return;

        // Cierra toda la sesión (familia) del token presentado, incluido un sucesor vivo
        // si el presentado ya estaba rotado. No pisa el motivo de los ya cerrados.
        var ahora   = DateTime.UtcNow;
        var familia = token.FamiliaId ?? token.Id;
        var activos = await _context.AuthTokens
            .Where(t => t.UserId == token.UserId && t.Tipo == PoliticaRenovacion.TipoRefresh && !t.Usado &&
                        (t.FamiliaId == familia || t.Id == familia))
            .ToListAsync();
        foreach (var t in activos) Cerrar(t, PoliticaRenovacion.Logout, ahora);

        if (!token.Usado) Cerrar(token, PoliticaRenovacion.Logout, ahora);
        await _context.SaveChangesAsync();
    }

    public async Task LogoutAllAsync(Guid userId)
    {
        var ahora = DateTime.UtcNow;
        var tokens = await _context.AuthTokens
            .Where(t => t.UserId == userId && t.Tipo == PoliticaRenovacion.TipoRefresh && !t.Usado)
            .ToListAsync();
        foreach (var t in tokens) Cerrar(t, PoliticaRenovacion.LogoutTodas, ahora);

        // Sin sesiones abiertas no debe llegar ningún push a sus teléfonos.
        var pushTokens = await _context.PushTokenDevices
            .Where(p => p.UserId == userId)
            .ToListAsync();
        _context.PushTokenDevices.RemoveRange(pushTokens);

        await _context.SaveChangesAsync();
    }

    private static void Cerrar(AuthToken t, string motivo, DateTime ahora)
    {
        t.Usado        = true;
        t.MotivoCierre = motivo;
        t.CerradoEn    = ahora;
    }

    // ── Forgot Password ───────────────────────────────────────────
    public async Task ForgotPasswordAsync(string correo)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Correo == correo.ToLower().Trim());

        // Siempre responde OK — no revelar si el correo existe
        if (user == null) return;

        // Invalidar tokens anteriores de recuperación
        var viejos = await _context.AuthTokens
            .Where(t => t.UserId == user.Id && t.Tipo == "PASSWORD_RESET" && !t.Usado)
            .ToListAsync();
        foreach (var t in viejos) t.Usado = true;

        // Generar token de recuperación (expira en 1 hora)
        var resetToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _context.AuthTokens.Add(new AuthToken
        {
            Id       = Guid.NewGuid(),
            UserId   = user.Id,
            Token    = resetToken,
            Tipo     = "PASSWORD_RESET",
            ExpiraEn = DateTime.UtcNow.AddHours(1),
            CreadoEn = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // TODO: Enviar correo con el token
        // Por ahora se loggea — integrar con IEmailService cuando esté disponible
        _logger.LogInformation("Token de recuperación para {Correo}: {Token}", user.Correo, resetToken);
    }

    // ── Reset Password ────────────────────────────────────────────
    public async Task ResetPasswordAsync(ResetPasswordRequestDto request)
    {
        var token = await _context.AuthTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t =>
                t.Token == request.Token && t.Tipo == "PASSWORD_RESET" &&
                !t.Usado && t.ExpiraEn > DateTime.UtcNow);

        if (token == null)
            throw new AppException("Token inválido o expirado.");

        token.User.PasswordHash  = BCrypt.Net.BCrypt.HashPassword(request.NuevaContrasena);
        token.User.ActualizadoEn = DateTime.UtcNow;
        token.Usado              = true;

        // Invalidar todos los refresh tokens activos por seguridad
        var refreshTokens = await _context.AuthTokens
            .Where(t => t.UserId == token.UserId && t.Tipo == PoliticaRenovacion.TipoRefresh && !t.Usado)
            .ToListAsync();
        var ahoraReset = DateTime.UtcNow;
        foreach (var rt in refreshTokens) Cerrar(rt, PoliticaRenovacion.ResetPassword, ahoraReset);

        await _context.SaveChangesAsync();
        _logger.LogInformation("Contraseña restablecida para usuario {Id}", token.UserId);
    }

    // ── Perfil ────────────────────────────────────────────────────
    public async Task<UserProfileDto> ObtenerPerfilAsync(Guid userId)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new NotFoundException("Usuario", userId);

        return MapToProfileDto(user);
    }

    public async Task<UserProfileDto> ActualizarPerfilAsync(Guid userId, UpdateProfileRequestDto request)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new NotFoundException("Usuario", userId);

        if (!string.IsNullOrWhiteSpace(request.Nombre))   user.Nombre   = request.Nombre.Trim();
        if (!string.IsNullOrWhiteSpace(request.Apellido)) user.Apellido = request.Apellido.Trim();
        if (!string.IsNullOrWhiteSpace(request.Telefono)) user.Telefono = request.Telefono.Trim();
        if (!string.IsNullOrWhiteSpace(request.Sexo))     user.Sexo     = request.Sexo.Trim();
        if (request.FechaNacimiento.HasValue)
            user.FechaNacimiento = request.FechaNacimiento;

        user.ActualizadoEn = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Perfil actualizado: {Id}", userId);
        return MapToProfileDto(user);
    }

    // ── Helpers ───────────────────────────────────────────────────
    private string GenerarAccessToken(User user, List<string> roles)
    {
        var key      = Env("JWT_KEY");
        var issuer   = Env("JWT_ISSUER");
        var audience = Env("JWT_AUDIENCE");
        var minutes  = int.TryParse(Environment.GetEnvironmentVariable("JWT_ACCESS_EXPIRATION_MINUTES"), out var m) ? m : 60;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Correo),
            new(ClaimTypes.NameIdentifier,     user.Id.ToString()),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var token = new JwtSecurityToken(
            issuer:             issuer,
            audience:           audience,
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            DateTime.UtcNow.AddMinutes(minutes),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Crea un refresh token. Sin <paramref name="familiaId"/> (login) inicia una familia
    /// nueva: su propio Id. En la rotación hereda la familia del token canjeado.
    /// </summary>
    private async Task<AuthToken> GuardarRefreshTokenAsync(Guid userId, Guid? familiaId = null)
    {
        var days  = int.TryParse(Environment.GetEnvironmentVariable("JWT_REFRESH_EXPIRATION_DAYS"), out var d) ? d : 365;
        var id    = Guid.NewGuid();
        var token = new AuthToken
        {
            Id        = id,
            UserId    = userId,
            Token     = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            Tipo      = PoliticaRenovacion.TipoRefresh,
            ExpiraEn  = DateTime.UtcNow.AddDays(days),
            CreadoEn  = DateTime.UtcNow,
            FamiliaId = familiaId ?? id
        };
        _context.AuthTokens.Add(token);
        await _context.SaveChangesAsync();
        return token;
    }

    // Ventana de reintento (segundos). 0 = rotación estricta. Ausente, no numérica o negativa
    // = 60; mayor de 300 se acota a 300.
    private static int LeerVentanaReusoSegundos()
    {
        const int porDefecto = 60;
        if (!int.TryParse(Environment.GetEnvironmentVariable("JWT_REFRESH_REUSE_WINDOW_SECONDS"), out var v) || v < 0)
            return porDefecto;
        return Math.Min(v, 300);
    }

    // Detección de reutilización (revoca la familia). Por defecto activa.
    private static bool LeerDeteccionReuso() =>
        !bool.TryParse(Environment.GetEnvironmentVariable("JWT_REFRESH_REUSE_DETECTION"), out var v) || v;

    private static LoginResponseDto BuildLoginResponse(User user, List<string> roles, string access, string refresh) =>
        new()
        {
            AccessToken  = access,
            RefreshToken = refresh,
            Usuario = new UsuarioDto
            {
                Id     = user.Id,
                Nombre = $"{user.Nombre} {user.Apellido}".Trim(),
                Correo = user.Correo ?? "",
                Roles  = roles
            }
        };

    private static UserProfileDto MapToProfileDto(User user) => new()
    {
        Id               = user.Id,
        Nombre           = user.Nombre,
        Apellido         = user.Apellido,
        Correo           = user.Correo,
        Telefono         = user.Telefono,
        Sexo             = user.Sexo,
        FechaNacimiento  = user.FechaNacimiento,
        Estado           = user.Estado,
        CorreoVerificado = user.CorreoVerificado,
        EsResponsableTurno = user.EsResponsableTurno,
        Roles            = user.UserRoles.Select(ur => ur.Role.Nombre).ToList(),
        CreadoEn         = user.CreadoEn
    };

    private static string Env(string key) =>
        Environment.GetEnvironmentVariable(key)
        ?? throw new InvalidOperationException($"Variable '{key}' no configurada en .env");
}
