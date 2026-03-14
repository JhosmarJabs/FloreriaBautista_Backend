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
    private readonly AppDbContext                _context;
    private readonly ILogger<AuthService>        _logger;

    public AuthService(AppDbContext context, ILogger<AuthService> logger)
    {
        _context = context;
        _logger  = logger;
    }

    // ── Login ─────────────────────────────────────────────────────
    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Correo == request.Correo.ToLower().Trim());

        if (user == null)
            throw new UnauthorizedException("Correo o contraseña incorrectos.");

        if (user.Estado != "ACTIVO")
            throw new UnauthorizedException("Tu cuenta está desactivada.");

        // Texto plano por ahora — cuando actives BCrypt reemplaza esta línea
        var passwordValida = user.PasswordHash == request.Contrasena;
        if (!passwordValida)
            throw new UnauthorizedException("Correo o contraseña incorrectos.");

        var roles = user.UserRoles.Select(ur => ur.Role.Nombre).ToList();

        var accessToken  = GenerarAccessToken(user, roles);
        var refreshToken = await GuardarRefreshTokenAsync(user.Id);

        _logger.LogInformation("Login exitoso: {Correo} | Roles: {Roles}",
            user.Correo, string.Join(", ", roles));

        return new LoginResponseDto
        {
            AccessToken  = accessToken,
            RefreshToken = refreshToken,
            Usuario = new UsuarioDto
            {
                Id     = user.Id,
                Nombre = $"{user.Nombre} {user.Apellido}".Trim(),
                Correo = user.Correo,
                Roles  = roles
            }
        };
    }

    // ── Refresh ───────────────────────────────────────────────────
    public async Task<LoginResponseDto> RefreshAsync(string refreshToken)
    {
        var token = await _context.AuthTokens
            .Include(t => t.User)
                .ThenInclude(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(t =>
                t.Token == refreshToken &&
                t.Tipo  == "REFRESH" &&
                !t.Usado &&
                t.ExpiraEn > DateTime.UtcNow);

        if (token == null)
            throw new UnauthorizedException("Refresh token inválido o expirado.");

        // Invalidar el token usado
        token.Usado = true;
        await _context.SaveChangesAsync();

        var user  = token.User;
        var roles = user.UserRoles.Select(ur => ur.Role.Nombre).ToList();

        var newAccessToken  = GenerarAccessToken(user, roles);
        var newRefreshToken = await GuardarRefreshTokenAsync(user.Id);

        return new LoginResponseDto
        {
            AccessToken  = newAccessToken,
            RefreshToken = newRefreshToken,
            Usuario = new UsuarioDto
            {
                Id     = user.Id,
                Nombre = $"{user.Nombre} {user.Apellido}".Trim(),
                Correo = user.Correo,
                Roles  = roles
            }
        };
    }

    // ── Logout ────────────────────────────────────────────────────
    public async Task LogoutAsync(string refreshToken)
    {
        var token = await _context.AuthTokens
            .FirstOrDefaultAsync(t => t.Token == refreshToken && t.Tipo == "REFRESH");

        if (token != null)
        {
            token.Usado = true;
            await _context.SaveChangesAsync();
        }
    }

    public async Task LogoutAllAsync(Guid userId)
    {
        var tokens = await _context.AuthTokens
            .Where(t => t.UserId == userId && t.Tipo == "REFRESH" && !t.Usado)
            .ToListAsync();

        foreach (var t in tokens) t.Usado = true;
        await _context.SaveChangesAsync();
    }

    // ── Register (cliente) ────────────────────────────────────────
    public async Task<LoginResponseDto> RegisterAsync(RegisterRequestDto request)
    {
        var existe = await _context.Users
            .AnyAsync(u => u.Correo == request.Correo.ToLower().Trim());

        if (existe)
            throw new AppException("Ya existe una cuenta con ese correo.");

        var rolCliente = await _context.Roles
            .FirstOrDefaultAsync(r => r.Nombre == "CLIENTE")
            ?? throw new AppException("Rol CLIENTE no encontrado. Ejecuta el DDL seed.");

        var user = new User
        {
            Id              = Guid.NewGuid(),
            Nombre          = request.Nombre.Trim(),
            Apellido        = request.Apellido.Trim(),
            Correo          = request.Correo.ToLower().Trim(),
            Telefono        = request.Telefono,
            PasswordHash    = request.Contrasena, // texto plano por ahora
            EsCliente       = true,
            Estado          = "ACTIVO",
            CorreoVerificado = false,
            CreadoEn        = DateTime.UtcNow,
            ActualizadoEn   = DateTime.UtcNow
        };

        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = rolCliente.Id });
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Nuevo cliente registrado: {Correo}", user.Correo);

        return await LoginAsync(new LoginRequestDto
        {
            Correo     = request.Correo,
            Contrasena = request.Contrasena
        });
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
                SecurityAlgorithms.HmacSha256)
        );

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
            Usado    = false,
            CreadoEn = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return token;
    }

    private static string Env(string key) =>
        Environment.GetEnvironmentVariable(key)
        ?? throw new InvalidOperationException($"Variable '{key}' no configurada en .env");
}
