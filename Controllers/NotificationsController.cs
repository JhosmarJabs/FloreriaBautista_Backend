using FloreriaBautista.Data;
using FloreriaBautista.Extensions;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Notifications;
using FloreriaBautista.Services.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificaciones;
    private readonly AppDbContext _context;

    public NotificationsController(INotificationService notificaciones, AppDbContext context)
    {
        _notificaciones = notificaciones;
        _context        = context;
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] bool soloNoLeidas = false,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var (usuarioId, customerId) = await ResolverDestinatario();
        var resultado = await _notificaciones.ListarAsync(usuarioId, customerId, soloNoLeidas, page, size);
        return Ok(ApiResponseDto<PagedResultDto<NotificationDto>>.Ok(resultado));
    }

    [HttpGet("no-leidas/count")]
    public async Task<IActionResult> ContarNoLeidas()
    {
        var (usuarioId, customerId) = await ResolverDestinatario();
        var count = await _notificaciones.ContarNoLeidasAsync(usuarioId, customerId);
        return Ok(ApiResponseDto<object>.Ok(new { count }));
    }

    [HttpPatch("{id:guid}/leer")]
    public async Task<IActionResult> MarcarLeida(Guid id)
    {
        var (usuarioId, customerId) = await ResolverDestinatario();
        await _notificaciones.MarcarLeidaAsync(id, usuarioId, customerId);
        return NoContent();
    }

    [HttpPatch("leer-todas")]
    public async Task<IActionResult> MarcarTodasLeidas()
    {
        var (usuarioId, customerId) = await ResolverDestinatario();
        await _notificaciones.MarcarTodasLeidasAsync(usuarioId, customerId);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Eliminar(Guid id)
    {
        var (usuarioId, customerId) = await ResolverDestinatario();
        await _notificaciones.EliminarAsync(id, usuarioId, customerId);
        return NoContent();
    }

    private async Task<(Guid? usuarioId, Guid? customerId)> ResolverDestinatario()
    {
        var userId = User.UsuarioIdRequerido();

        if (User.IsInRole("CLIENTE"))
        {
            var customer = await _context.Customers
                .Where(c => c.UserId == userId)
                .Select(c => c.Id)
                .FirstOrDefaultAsync();

            if (customer != Guid.Empty)
                return (null, customer);
        }

        return (userId, null);
    }
}
