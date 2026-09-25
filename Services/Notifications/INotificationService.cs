using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Notifications;

namespace FloreriaBautista.Services.Notifications;

public interface INotificationService
{
    Task CrearParaUsuariosAsync(string tipo, string titulo, string mensaje,
        string[] roles, string? entidadTipo = null, Guid? entidadId = null);

    Task CrearParaClienteAsync(Guid customerId, string tipo, string titulo,
        string mensaje, string? entidadTipo = null, Guid? entidadId = null);

    Task<PagedResultDto<NotificationDto>> ListarAsync(
        Guid? usuarioId, Guid? customerId, bool soloNoLeidas, int page, int size);

    Task<int> ContarNoLeidasAsync(Guid? usuarioId, Guid? customerId);

    Task MarcarLeidaAsync(Guid notificationId, Guid? usuarioId, Guid? customerId);

    Task MarcarTodasLeidasAsync(Guid? usuarioId, Guid? customerId);

    Task EliminarAsync(Guid notificationId, Guid? usuarioId, Guid? customerId);

    Task<bool> ExisteNoLeidaRecienteAsync(string tipo, Guid? entidadId, TimeSpan ventana);
}
