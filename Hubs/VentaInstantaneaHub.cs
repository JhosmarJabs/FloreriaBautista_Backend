using System.Security.Claims;
using FloreriaBautista.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Hubs;

[Authorize]
public class VentaInstantaneaHub : Hub
{
    private readonly AppDbContext _context;

    public VentaInstantaneaHub(AppDbContext context)
    {
        _context = context;
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
    /// recibir la decision en tiempo real.
    /// </summary>
    public async Task SuscribirSolicitud(string solicitudId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"solicitud-{solicitudId}");
    }
}
