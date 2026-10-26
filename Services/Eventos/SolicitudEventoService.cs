using System.Linq.Expressions;
using System.Text.RegularExpressions;
using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Eventos;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Services.Interfaces;
using FloreriaBautista.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Services.Eventos;

public class SolicitudEventoService : ISolicitudEventoService
{
    public const int MaxPendientesPorCliente = 3;
    private const int MaxInvitados = 5000;

    private static readonly Regex FormatoCorreo =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Expression<Func<SolicitudEvento, SolicitudEventoDto>> ADto = s => new SolicitudEventoDto
    {
        Id               = s.Id,
        CustomerId       = s.CustomerId,
        ClienteNombre    = s.Customer == null ? "" : (s.Customer.Nombre + " " + (s.Customer.Apellido ?? "")).Trim(),
        TipoEvento       = s.TipoEvento,
        FechaEvento      = s.FechaEvento,
        InvitadosAprox   = s.InvitadosAprox,
        NombreContacto   = s.NombreContacto,
        TelefonoContacto = s.TelefonoContacto,
        CorreoContacto   = s.CorreoContacto,
        Mensaje          = s.Mensaje,
        Estado           = s.Estado,
        CreadoEn         = s.CreadoEn,
        ActualizadoEn    = s.ActualizadoEn,
    };

    private readonly AppDbContext _db;
    private readonly INotificationService _notificaciones;
    private readonly IFechaHelper _fecha;
    private readonly ILogger<SolicitudEventoService> _logger;

    public SolicitudEventoService(AppDbContext db, INotificationService notificaciones,
        IFechaHelper fecha, ILogger<SolicitudEventoService> logger)
    {
        _db             = db;
        _notificaciones = notificaciones;
        _fecha          = fecha;
        _logger         = logger;
    }

    public async Task<SolicitudEventoDto> CrearAsync(Guid customerId, CrearSolicitudEventoDto dto)
    {
        // 1) Normalización
        var tipo     = (dto.TipoEvento ?? string.Empty).Trim().ToUpperInvariant();
        var nombre   = (dto.NombreContacto ?? string.Empty).Trim();
        var telefono = NormalizarTelefono(dto.TelefonoContacto);
        var correo   = string.IsNullOrWhiteSpace(dto.CorreoContacto) ? null : dto.CorreoContacto.Trim();
        var mensaje  = string.IsNullOrWhiteSpace(dto.Mensaje) ? null : dto.Mensaje.Trim();

        // 2) Validación
        if (!TiposEventoSolicitud.Todos.Contains(tipo))
            throw new AppException("El tipo de evento no es válido.");

        var hoy = _fecha.HoyLocal();
        if (dto.FechaEvento <= hoy)
            throw new AppException("La fecha del evento debe ser a partir de mañana.");
        if (dto.FechaEvento > hoy.AddYears(2))
            throw new AppException("La fecha del evento no puede ser mayor a 2 años.");

        if (dto.InvitadosAprox is < 1 or > MaxInvitados)
            throw new AppException($"Los invitados deben estar entre 1 y {MaxInvitados}.");

        if (nombre.Length is < 2 or > 150)
            throw new AppException("El nombre de contacto debe tener entre 2 y 150 caracteres.");

        if (telefono.Length != 10 || !telefono.All(char.IsAsciiDigit))
            throw new AppException("El teléfono debe tener 10 dígitos.");

        if (correo != null && (correo.Length > 150 || !FormatoCorreo.IsMatch(correo)))
            throw new AppException("El correo no tiene un formato válido.");

        if (mensaje != null && mensaje.Length > 1000)
            throw new AppException("El mensaje no puede superar los 1000 caracteres.");

        // 3) Límite de solicitudes en espera
        var pendientes = await _db.SolicitudesEvento
            .CountAsync(s => s.CustomerId == customerId && s.Estado == EstadosSolicitudEvento.Pendiente);
        if (pendientes >= MaxPendientesPorCliente)
            throw new AppException("Ya tienes 3 solicitudes en espera; te contactaremos pronto.");

        // 4) Guardado
        var ahora = _fecha.AhoraUtc();
        var solicitud = new SolicitudEvento
        {
            Id               = Guid.NewGuid(),
            CustomerId       = customerId,
            TipoEvento       = tipo,
            FechaEvento      = dto.FechaEvento,
            InvitadosAprox   = dto.InvitadosAprox,
            NombreContacto   = nombre,
            TelefonoContacto = telefono,
            CorreoContacto   = correo,
            Mensaje          = mensaje,
            Estado           = EstadosSolicitudEvento.Pendiente,
            CreadoEn         = ahora,
            ActualizadoEn    = ahora,
        };
        _db.SolicitudesEvento.Add(solicitud);
        await _db.SaveChangesAsync();

        // 5) Aviso a los ADMIN (solo el nombre; nunca teléfono ni correo)
        await NotificarAdminsAsync(solicitud);

        return await ObtenerDtoAsync(solicitud.Id);
    }

    public async Task<PagedResultDto<SolicitudEventoDto>> ListarAsync(string? estado, int page, int size)
    {
        page = Math.Max(page, 1);
        size = Math.Clamp(size, 1, 100);

        var query = _db.SolicitudesEvento.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(estado))
        {
            var e = estado.Trim().ToUpperInvariant();
            if (!EstadosSolicitudEvento.Todos.Contains(e))
                throw new AppException("El estado no es válido.");
            query = query.Where(s => s.Estado == e);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(s => s.CreadoEn)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(ADto)
            .ToListAsync();

        return new PagedResultDto<SolicitudEventoDto>
        {
            Items        = items,
            Total        = total,
            Pagina       = page,
            TamanoPagina = size,
            TotalPaginas = (int)Math.Ceiling(total / (double)size),
        };
    }

    public async Task<SolicitudEventoDto> CambiarEstadoAsync(Guid id, string estado)
    {
        var nuevo = (estado ?? string.Empty).Trim().ToUpperInvariant();
        if (!EstadosSolicitudEvento.Todos.Contains(nuevo))
            throw new AppException("El estado no es válido.");

        var solicitud = await _db.SolicitudesEvento.FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new NotFoundException("Solicitud de evento", id);

        solicitud.Estado        = nuevo;
        solicitud.ActualizadoEn = _fecha.AhoraUtc();
        await _db.SaveChangesAsync();

        return await ObtenerDtoAsync(id);
    }

    private async Task NotificarAdminsAsync(SolicitudEvento s)
    {
        try
        {
            var invitados = s.InvitadosAprox.HasValue ? $" · {s.InvitadosAprox} invitados" : "";
            var mensaje = $"{TipoLegible(s.TipoEvento)} el {s.FechaEvento:dd/MM/yyyy}{invitados} · {s.NombreContacto}";
            await _notificaciones.CrearParaUsuariosAsync(
                "SOLICITUD_EVENTO", "Nueva solicitud de evento", mensaje,
                ["ADMIN"], "SolicitudEvento", s.Id);
        }
        catch (Exception ex)
        {
            // La solicitud ya está guardada: la lista admin es la fuente de verdad.
            foreach (var entrada in _db.ChangeTracker.Entries<Notification>()
                         .Where(e => e.State == EntityState.Added).ToList())
                entrada.State = EntityState.Detached;
            _logger.LogWarning(ex, "No se pudo notificar la solicitud de evento {SolicitudId}", s.Id);
        }
    }

    private Task<SolicitudEventoDto> ObtenerDtoAsync(Guid id) =>
        _db.SolicitudesEvento.AsNoTracking().Where(s => s.Id == id).Select(ADto).FirstAsync();

    /// <summary>Quita espacios, guiones y un +52 inicial para validar los 10 dígitos.</summary>
    private static string NormalizarTelefono(string? telefono)
    {
        var t = (telefono ?? string.Empty).Replace(" ", "").Replace("-", "");
        if (t.StartsWith("+52", StringComparison.Ordinal)) t = t[3..];
        return t;
    }

    private static string TipoLegible(string tipo) => tipo switch
    {
        TiposEventoSolicitud.Boda          => "Boda",
        TiposEventoSolicitud.Bautizo       => "Bautizo",
        TiposEventoSolicitud.Corporativo   => "Evento corporativo",
        TiposEventoSolicitud.Conmemorativo => "Evento conmemorativo",
        _                                  => "Evento",
    };
}
