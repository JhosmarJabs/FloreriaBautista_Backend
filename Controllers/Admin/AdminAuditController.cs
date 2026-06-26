using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Audit;
using FloreriaBautista.Models.DTOs.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Controllers.Admin;

[ApiController]
[Tags("3. Sistema y Seguridad")]
[Route("api/admin/audit")]
[Authorize(Roles = "ADMIN")]
public class AdminAuditController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminAuditController(AppDbContext context) => _context = context;

    // GET /api/admin/audit
    // Filtros: entidad, accion, usuarioId, desde, hasta, page, size
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string?   entidad,
        [FromQuery] string?   accion,
        [FromQuery] Guid?     usuarioId,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] int       page = 1,
        [FromQuery] int       size = 50)
    {
        var query = _context.AuditLogs
            .Include(a => a.Usuario)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(entidad))
            query = query.Where(a => a.Entidad.ToLower() == entidad.ToLower().Trim());

        if (!string.IsNullOrWhiteSpace(accion))
            query = query.Where(a => a.Accion.ToLower() == accion.ToLower().Trim());

        if (usuarioId.HasValue)
            query = query.Where(a => a.UsuarioId == usuarioId);

        if (desde.HasValue)
            query = query.Where(a => a.FechaHora >= desde.Value.ToUniversalTime());

        if (hasta.HasValue)
            query = query.Where(a => a.FechaHora <= hasta.Value.ToUniversalTime());

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(a => a.FechaHora)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(a => new AuditLogDto
            {
                Id            = a.Id,
                UsuarioId     = a.UsuarioId,
                UsuarioNombre = a.Usuario != null ? $"{a.Usuario.Nombre} {a.Usuario.Apellido}" : null,
                UsuarioCorreo = a.Usuario != null ? a.Usuario.Correo : null,
                Accion        = a.Accion,
                Entidad       = a.Entidad,
                EntidadId     = a.EntidadId,
                Detalles      = a.Detalles,
                FechaHora     = a.FechaHora
            })
            .ToListAsync();

        return Ok(ApiResponseDto<PagedResultDto<AuditLogDto>>.Ok(new PagedResultDto<AuditLogDto>
        {
            Items        = items,
            Total        = total,
            Pagina       = page,
            TamanoPagina = size,
            TotalPaginas = (int)Math.Ceiling(total / (double)size)
        }));
    }

    // GET /api/admin/audit/{entidad}/{entidadId}
    // Historial completo de un registro específico
    [HttpGet("{entidad}/{entidadId}")]
    public async Task<IActionResult> HistorialEntidad(string entidad, string entidadId)
    {
        var items = await _context.AuditLogs
            .Include(a => a.Usuario)
            .Where(a => a.Entidad.ToLower() == entidad.ToLower()
                     && a.EntidadId         == entidadId)
            .OrderByDescending(a => a.FechaHora)
            .Select(a => new AuditLogDto
            {
                Id            = a.Id,
                UsuarioId     = a.UsuarioId,
                UsuarioNombre = a.Usuario != null ? $"{a.Usuario.Nombre} {a.Usuario.Apellido}" : null,
                UsuarioCorreo = a.Usuario != null ? a.Usuario.Correo : null,
                Accion        = a.Accion,
                Entidad       = a.Entidad,
                EntidadId     = a.EntidadId,
                Detalles      = a.Detalles,
                FechaHora     = a.FechaHora
            })
            .ToListAsync();

        return Ok(ApiResponseDto<List<AuditLogDto>>.Ok(items));
    }
}
