using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Auth;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Users;
using FloreriaBautista.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "ADMIN")]
public class AdminUsersController : ControllerBase
{
    private readonly AppDbContext _context;
    public AdminUsersController(AppDbContext context) => _context = context;

    // POST /api/admin/users — crear usuario interno con roles
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CreateInternalUserRequestDto request)
    {
        var existe = await _context.Users.AnyAsync(u => u.Correo == request.Correo.ToLower().Trim());
        if (existe)
            return Conflict(ApiResponseDto<object>.Fail("Ya existe un usuario con ese correo."));

        var rolesDb = await _context.Roles
            .Where(r => request.Roles.Contains(r.Nombre))
            .ToListAsync();

        if (rolesDb.Count != request.Roles.Count)
        {
            var invalidos = request.Roles.Except(rolesDb.Select(r => r.Nombre));
            return BadRequest(ApiResponseDto<object>.Fail(
                $"Roles no encontrados: {string.Join(", ", invalidos)}"));
        }

        var user = new User
        {
            Id               = Guid.NewGuid(),
            Nombre           = request.Nombre.Trim(),
            ApellidoP        = request.ApellidoP.Trim(),
            ApellidoM        = request.ApellidoM?.Trim(),
            Correo           = request.Correo.ToLower().Trim(),
            Telefono         = request.Telefono,
            PasswordHash     = BCrypt.Net.BCrypt.HashPassword(request.Password),
            EsCliente        = request.Roles.Contains("CLIENTE"),
            Estado           = "ACTIVO",
            CorreoVerificado = false,
            CreadoEn         = DateTime.UtcNow,
            ActualizadoEn    = DateTime.UtcNow
        };

        foreach (var rol in rolesDb)
            user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = rol.Id });

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok(ApiResponseDto<UserProfileDto>.Ok(new UserProfileDto
        {
            Id       = user.Id,
            Nombre   = user.Nombre,
            ApellidoP = user.ApellidoP,
            ApellidoM = user.ApellidoM,
            Correo   = user.Correo,
            Telefono = user.Telefono,
            Estado   = user.Estado,
            Roles    = rolesDb.Select(r => r.Nombre).ToList(),
            CreadoEn = user.CreadoEn
        }));
    }

    // GET /api/admin/users?busqueda=&rol=ADMIN&estado=ACTIVO&page=1&size=20
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string? busqueda,
        [FromQuery] string? rol,
        [FromQuery] string? estado,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var query = _context.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(busqueda))
            query = query.Where(u =>
                u.Nombre.Contains(busqueda) ||
                u.ApellidoP.Contains(busqueda) ||
                u.Correo.Contains(busqueda));

        if (!string.IsNullOrWhiteSpace(rol))
            query = query.Where(u =>
                u.UserRoles.Any(ur => ur.Role.Nombre == rol.ToUpper()));

        if (!string.IsNullOrWhiteSpace(estado))
            query = query.Where(u => u.Estado == estado.ToUpper());

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(u => u.Nombre)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(u => new UserProfileDto
            {
                Id               = u.Id,
                Nombre           = u.Nombre,
                ApellidoP        = u.ApellidoP,
                ApellidoM        = u.ApellidoM,
                Correo           = u.Correo,
                Telefono         = u.Telefono,
                Sexo             = u.Sexo,
                Estado           = u.Estado,
                CorreoVerificado = u.CorreoVerificado,
                Roles            = u.UserRoles.Select(ur => ur.Role.Nombre).ToList(),
                CreadoEn         = u.CreadoEn
            })
            .ToListAsync();

        return Ok(ApiResponseDto<PagedResultDto<UserProfileDto>>.Ok(
            new PagedResultDto<UserProfileDto>
            {
                Items        = items,
                Total        = total,
                Pagina       = page,
                TamanoPagina = size,
                TotalPaginas = (int)Math.Ceiling(total / (double)size)
            }));
    }

    // GET /api/admin/users/{userId}
    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> Detalle(Guid userId)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return NotFound(ApiResponseDto<object>.Fail($"Usuario '{userId}' no encontrado."));

        return Ok(ApiResponseDto<UserProfileDto>.Ok(new UserProfileDto
        {
            Id               = user.Id,
            Nombre           = user.Nombre,
            ApellidoP        = user.ApellidoP,
            ApellidoM        = user.ApellidoM,
            Correo           = user.Correo,
            Telefono         = user.Telefono,
            Sexo             = user.Sexo,
            FechaNacimiento  = user.FechaNacimiento,
            Estado           = user.Estado,
            CorreoVerificado = user.CorreoVerificado,
            Roles            = user.UserRoles.Select(ur => ur.Role.Nombre).ToList(),
            CreadoEn         = user.CreadoEn
        }));
    }

    // POST /api/admin/users/{userId}/status — activar/desactivar
    [HttpPost("{userId:guid}/status")]
    public async Task<IActionResult> CambiarEstado(Guid userId, [FromBody] UpdateStatusRequestDto request)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return NotFound(ApiResponseDto<object>.Fail($"Usuario '{userId}' no encontrado."));

        user.Estado        = request.Activo ? "ACTIVO" : "INACTIVO";
        user.ActualizadoEn = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(ApiResponseDto<object>.Ok(null,
            $"Usuario {(request.Activo ? "activado" : "desactivado")} correctamente."));
    }

    // POST /api/admin/users/{userId}/roles — actualizar roles
    [HttpPost("{userId:guid}/roles")]
    public async Task<IActionResult> ActualizarRoles(Guid userId, [FromBody] UpdateRolesRequestDto request)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return NotFound(ApiResponseDto<object>.Fail($"Usuario '{userId}' no encontrado."));

        var rolesDb = await _context.Roles
            .Where(r => request.Roles.Contains(r.Nombre))
            .ToListAsync();

        if (rolesDb.Count != request.Roles.Count)
        {
            var invalidos = request.Roles.Except(rolesDb.Select(r => r.Nombre));
            return BadRequest(ApiResponseDto<object>.Fail(
                $"Roles no encontrados: {string.Join(", ", invalidos)}"));
        }

        _context.RemoveRange(user.UserRoles);
        foreach (var rol in rolesDb)
            _context.Add(new FloreriaBautista.Models.Entities.UserRole
                { UserId = userId, RoleId = rol.Id });

        await _context.SaveChangesAsync();
        return Ok(ApiResponseDto<object>.Ok(null, "Roles actualizados correctamente."));
    }
}

public class UpdateStatusRequestDto
{
    public bool   Activo { get; set; }
    public string? Motivo { get; set; }
}

public class UpdateRolesRequestDto
{
    public List<string> Roles { get; set; } = [];
}
