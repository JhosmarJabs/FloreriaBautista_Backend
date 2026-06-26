using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Auth;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Users;
using FloreriaBautista.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Controllers.Admin;

[ApiController]
[Tags("2. Operaciones y Ventas")]
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

        var rolEmpleado = await _context.Roles.FirstOrDefaultAsync(r => r.Nombre == "EMPLEADO");
        if (rolEmpleado == null)
            return StatusCode(500, ApiResponseDto<object>.Fail("Rol EMPLEADO no encontrado en la base de datos."));

        var user = new User
        {
            Id               = Guid.NewGuid(),
            Nombre           = request.Nombre.Trim(),
            Apellido         = request.Apellido.Trim(),
            Correo           = request.Correo.ToLower().Trim(),
            Telefono         = request.Telefono,
            PasswordHash     = BCrypt.Net.BCrypt.HashPassword(request.Password),
            EsCliente        = false,
            Estado           = "ACTIVO",
            CorreoVerificado = false,
            CreadoEn         = DateTime.UtcNow,
            ActualizadoEn    = DateTime.UtcNow
        };

        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = rolEmpleado.Id });

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok(ApiResponseDto<UserProfileDto>.Ok(new UserProfileDto
        {
            Id       = user.Id,
            Nombre   = user.Nombre,
            Apellido = user.Apellido,
            Correo   = user.Correo,
            Telefono = user.Telefono,
            Estado   = user.Estado,
            Roles    = ["EMPLEADO"],
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
                u.Apellido.Contains(busqueda) ||
                (u.Correo != null && u.Correo.Contains(busqueda)));

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
                Apellido         = u.Apellido,
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
            Apellido         = user.Apellido,
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

    // POST /api/admin/users/{userId:guid} — actualizar perfil, estado y roles (Admin)
    [HttpPost("{userId:guid}")]
    public async Task<IActionResult> Actualizar(Guid userId, [FromBody] UpdateUserRequestDto request)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return NotFound(ApiResponseDto<object>.Fail($"Usuario '{userId}' no encontrado."));

        // 1. Actualización de datos básicos
        if (request.Nombre != null) user.Nombre = request.Nombre.Trim();
        if (request.Apellido != null) user.Apellido = request.Apellido.Trim();
        if (request.Telefono != null) user.Telefono = request.Telefono;
        if (request.Sexo != null) user.Sexo = request.Sexo;
        if (request.FechaNacimiento != null) 
            user.FechaNacimiento = DateOnly.FromDateTime(request.FechaNacimiento.Value);

        // 2. Estado (Borrado lógico)
        if (request.Activo.HasValue)
            user.Estado = request.Activo.Value ? "ACTIVO" : "INACTIVO";

        // 3. Roles
        if (request.Roles != null)
        {
            var rolesDb = await _context.Roles
                .Where(r => request.Roles.Contains(r.Nombre))
                .ToListAsync();

            if (rolesDb.Count != request.Roles.Count)
            {
                var invalidos = request.Roles.Except(rolesDb.Select(r => r.Nombre));
                return BadRequest(ApiResponseDto<object>.Fail($"Roles no encontrados: {string.Join(", ", invalidos)}"));
            }

            _context.RemoveRange(user.UserRoles);
            foreach (var rol in rolesDb)
                _context.Add(new UserRole { UserId = userId, RoleId = rol.Id });
        }

        user.ActualizadoEn = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(ApiResponseDto<object>.Ok(null, "Usuario actualizado correctamente."));
    }
}
