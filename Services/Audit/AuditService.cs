using System.Text.Json;
using FloreriaBautista.Data;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Services.Interfaces;

namespace FloreriaBautista.Services.Audit;

public class AuditService : IAuditService
{
    private readonly AppDbContext            _context;
    private readonly ILogger<AuditService>  _logger;

    public AuditService(AppDbContext context, ILogger<AuditService> logger)
    {
        _context = context;
        _logger  = logger;
    }

    public async Task RegistrarAsync(
        string  accion,
        string  entidad,
        string? entidadId,
        Guid?   usuarioId,
        object? detalles = null)
    {
        try
        {
            _context.AuditLogs.Add(new AuditLog
            {
                Id        = Guid.NewGuid(),
                Accion    = accion.ToUpper().Trim(),
                Entidad   = entidad.Trim(),
                EntidadId = entidadId,
                UsuarioId = usuarioId,
                Detalles  = detalles is null ? null : JsonSerializer.Serialize(detalles),
                FechaHora = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Nunca debe bloquear la operación principal
            _logger.LogWarning(ex, "Error al registrar audit log: {Accion} {Entidad}", accion, entidad);
        }
    }
}
