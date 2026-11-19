using System.Security.Claims;
using FloreriaBautista.Data;
using FloreriaBautista.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Hubs;

// Exige JWT; el token llega por ?access_token= (ServiceExtensions). Sin roles:
// staff y clientes comparten el hub; la regla por rol va en cada metodo.
[Authorize]
public class VentaInstantaneaHub : Hub
{
    private readonly AppDbContext _context;
    private readonly ILogger<VentaInstantaneaHub> _logger;

    public VentaInstantaneaHub(AppDbContext context, ILogger<VentaInstantaneaHub> logger)
    {
        _context = context;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst("sub")?.Value
                  ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userId == null)
        {
            await base.OnConnectedAsync();
            return;
        }

        var parsedId = Guid.TryParse(userId, out var uid) ? uid : (Guid?)null;

        if (parsedId.HasValue)
        {
            var user = await _context.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == parsedId.Value);

            if (user != null)
            {
                var esAdmin = user.UserRoles.Any(ur =>
                    ur.Role.Nombre.Equals("ADMIN", StringComparison.OrdinalIgnoreCase));
                var esEmpleado = user.UserRoles.Any(ur =>
                    ur.Role.Nombre.Equals("EMPLEADO", StringComparison.OrdinalIgnoreCase));

                if (esAdmin)
                    await Groups.AddToGroupAsync(Context.ConnectionId, "admin");

                if (esEmpleado || esAdmin)
                    await Groups.AddToGroupAsync(Context.ConnectionId, "todos-empleados");

                if (user.EsResponsableTurno)
                    await Groups.AddToGroupAsync(Context.ConnectionId, "responsable");
            }
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// El cliente (rol CLIENTE) se suscribe al grupo de su solicitud para
    /// recibir la decision en tiempo real. Solo el dueño de la solicitud puede
    /// hacerlo; "no existe" y "no es tuya" responden igual para no permitir
    /// enumerar solicitudes ajenas.
    /// </summary>
    public async Task SuscribirSolicitud(string solicitudId)
    {
        if (!Guid.TryParse(solicitudId, out var id))
            throw Rechazo("formato", "Solicitud no válida.");

        var userId = Context.User?.UsuarioId();
        if (userId == null)
            throw Rechazo("usuario", "No autorizado.");

        if (!Context.User!.IsInRole("CLIENTE"))
            throw Rechazo("rol", "No autorizado.", userId);

        var esDueno = await _context.SolicitudesVentaInstantanea
            .AsNoTracking()
            .AnyAsync(s => s.Id == id && s.Customer.UserId == userId);
        if (!esDueno)
            throw Rechazo("propiedad", "No autorizado.", userId);

        // El Guid parseado (formato D, minusculas) coincide con el que interpola RealtimeNotifier.
        await Groups.AddToGroupAsync(Context.ConnectionId, $"solicitud-{id}");
    }

    private HubException Rechazo(string motivo, string mensaje, Guid? userId = null)
    {
        _logger.LogWarning(
            "SuscribirSolicitud rechazada. ConnectionId={ConnectionId} UserId={UserId} Motivo={Motivo}",
            Context.ConnectionId, userId, motivo);
        return new HubException(mensaje);
    }
}
