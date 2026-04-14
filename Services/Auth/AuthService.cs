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
        var refreshToken = await GuardarRefreshTokenAsync(user.Id);

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
    public async Task<LoginResponseDto> RefreshAsync(string refreshToken)
    {
        var token = await _context.AuthTokens
            .Include(t => t.User).ThenInclude(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(t =>
                t.Token == refreshToken && t.Tipo == "REFRESH" &&
                !t.Usado && t.ExpiraEn > DateTime.UtcNow);

        if (token == null)
            throw new UnauthorizedException("Refresh token inválido o expirado.");

        token.Usado = true;
        await _context.SaveChangesAsync();

        var user  = token.User;
        var roles = user.UserRoles.Select(ur => ur.Role.Nombre).ToList();
        var newAccess  = GenerarAccessToken(user, roles);
        var newRefresh = await GuardarRefreshTokenAsync(user.Id);

        return BuildLoginResponse(user, roles, newAccess, newRefresh);
    }

    // ── Logout ────────────────────────────────────────────────────
    public async Task LogoutAsync(string refreshToken)
    {
        var token = await _context.AuthTokens
            .FirstOrDefaultAsync(t => t.Token == refreshToken && t.Tipo == "REFRESH");
        if (token != null) { token.Usado = true; await _context.SaveChangesAsync(); }
    }

    public async Task LogoutAllAsync(Guid userId)
    {
        var tokens = await _context.AuthTokens
            .Where(t => t.UserId == userId && t.Tipo == "REFRESH" && !t.Usado)
            .ToListAsync();
        foreach (var t in tokens) t.Usado = true;
        await _context.SaveChangesAsync();
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
            .Where(t => t.UserId == token.UserId && t.Tipo == "REFRESH" && !t.Usado)
            .ToListAsync();
        foreach (var rt in refreshTokens) rt.Usado = true;

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

    private async Task<string> GuardarRefreshTokenAsync(Guid userId)
    {
        var days  = int.TryParse(Environment.GetEnvironmentVariable("JWT_REFRESH_EXPIRATION_DAYS"), out var d) ? d : 7;
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        _context.AuthTokens.Add(new AuthToken
        {
            Id       = Guid.NewGuid(),
            UserId   = userId,
            Token    = token,
            Tipo     = "REFRESH",
            ExpiraEn = DateTime.UtcNow.AddDays(days),
            CreadoEn = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
        return token;
    }

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
        Roles            = user.UserRoles.Select(ur => ur.Role.Nombre).ToList(),
        CreadoEn         = user.CreadoEn
    };

    private static string Env(string key) =>
        Environment.GetEnvironmentVariable(key)
        ?? throw new InvalidOperationException($"Variable '{key}' no configurada en .env");
}
