using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Notifications;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Services.Notifications;

public class NotificationService : INotificationService
{
    private readonly AppDbContext _context;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(AppDbContext context, ILogger<NotificationService> logger)
    {
        _context = context;
        _logger  = logger;
    }

    public async Task CrearParaUsuariosAsync(string tipo, string titulo, string mensaje,
        string[] roles, string? entidadTipo = null, Guid? entidadId = null)
    {
        var destinatarios = await _context.Users
            .Where(u => u.Estado == "ACTIVO"
                && u.UserRoles.Any(ur => roles.Contains(ur.Role.Nombre)))
            .Select(u => u.Id)
            .ToListAsync();

        if (destinatarios.Count == 0)
        {
            _logger.LogWarning("Notificación {Tipo} sin destinatarios: no hay usuarios activos con roles {Roles}",
                tipo, string.Join(", ", roles));
            return;
        }

        var ahora = DateTime.UtcNow;
        foreach (var uid in destinatarios)
        {
            _context.Notifications.Add(new Notification
            {
                Id                    = Guid.NewGuid(),
                DestinatarioUsuarioId = uid,
                Tipo                  = tipo,
                Titulo                = titulo,
                Mensaje               = mensaje,
                EntidadTipo           = entidadTipo,
                EntidadId             = entidadId,
                CreadaEn              = ahora,
            });
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Notificación {Tipo} creada para {Count} usuario(s)", tipo, destinatarios.Count);
    }

    public async Task CrearParaClienteAsync(Guid customerId, string tipo, string titulo,
        string mensaje, string? entidadTipo = null, Guid? entidadId = null)
    {
        _context.Notifications.Add(new Notification
        {
            Id                     = Guid.NewGuid(),
            DestinatarioCustomerId = customerId,
            Tipo                   = tipo,
            Titulo                 = titulo,
            Mensaje                = mensaje,
            EntidadTipo            = entidadTipo,
            EntidadId              = entidadId,
            CreadaEn               = DateTime.UtcNow,
        });

        await _context.SaveChangesAsync();
        _logger.LogInformation("Notificación {Tipo} creada para cliente {CustomerId}", tipo, customerId);
    }

    public async Task<PagedResultDto<NotificationDto>> ListarAsync(
        Guid? usuarioId, Guid? customerId, bool soloNoLeidas, int page, int size)
    {
        var query = BuildQuery(usuarioId, customerId);

        if (soloNoLeidas)
            query = query.Where(n => !n.Leida);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(n => n.CreadaEn)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(n => MapToDto(n))
            .ToListAsync();

        return new PagedResultDto<NotificationDto>
        {
            Items        = items,
            Total        = total,
            Pagina       = page,
            TamanoPagina = size,
            TotalPaginas = (int)Math.Ceiling(total / (double)size)
        };
    }

    public async Task<int> ContarNoLeidasAsync(Guid? usuarioId, Guid? customerId)
    {
        return await BuildQuery(usuarioId, customerId)
            .Where(n => !n.Leida)
            .CountAsync();
    }

    public async Task MarcarLeidaAsync(Guid notificationId, Guid? usuarioId, Guid? customerId)
    {
        var notif = await _context.Notifications.FindAsync(notificationId)
            ?? throw new NotFoundException("Notification", notificationId);

        if (!EsDestinatario(notif, usuarioId, customerId))
            throw new ForbiddenException("No tiene permiso para modificar esta notificación.");

        if (!notif.Leida)
        {
            notif.Leida   = true;
            notif.LeidaEn = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task MarcarTodasLeidasAsync(Guid? usuarioId, Guid? customerId)
    {
        var ahora = DateTime.UtcNow;
        var noLeidas = await BuildQuery(usuarioId, customerId)
            .Where(n => !n.Leida)
            .ToListAsync();

        foreach (var n in noLeidas)
        {
            n.Leida   = true;
            n.LeidaEn = ahora;
        }

        await _context.SaveChangesAsync();
    }

    public async Task EliminarAsync(Guid notificationId, Guid? usuarioId, Guid? customerId)
    {
        var notif = await _context.Notifications.FindAsync(notificationId)
            ?? throw new NotFoundException("Notification", notificationId);

        if (!EsDestinatario(notif, usuarioId, customerId))
            throw new ForbiddenException("No tiene permiso para eliminar esta notificación.");

        _context.Notifications.Remove(notif);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExisteNoLeidaRecienteAsync(string tipo, Guid? entidadId, TimeSpan ventana)
    {
        var desde = DateTime.UtcNow - ventana;
        return await _context.Notifications
            .AnyAsync(n => n.Tipo == tipo
                && n.EntidadId == entidadId
                && !n.Leida
                && n.CreadaEn >= desde);
    }

    private IQueryable<Notification> BuildQuery(Guid? usuarioId, Guid? customerId)
    {
        if (usuarioId.HasValue)
            return _context.Notifications.Where(n => n.DestinatarioUsuarioId == usuarioId.Value);
        if (customerId.HasValue)
            return _context.Notifications.Where(n => n.DestinatarioCustomerId == customerId.Value);
        throw new AppException("Debe especificar un destinatario.");
    }

    private static bool EsDestinatario(Notification notif, Guid? usuarioId, Guid? customerId)
    {
        if (usuarioId.HasValue)
            return notif.DestinatarioUsuarioId == usuarioId.Value;
        if (customerId.HasValue)
            return notif.DestinatarioCustomerId == customerId.Value;
        return false;
    }

    private static NotificationDto MapToDto(Notification n) => new()
    {
        Id          = n.Id,
        Tipo        = n.Tipo,
        Titulo      = n.Titulo,
        Mensaje     = n.Mensaje,
        EntidadTipo = n.EntidadTipo,
        EntidadId   = n.EntidadId,
        Leida       = n.Leida,
        LeidaEn     = n.LeidaEn,
        CreadaEn    = n.CreadaEn,
    };
}
