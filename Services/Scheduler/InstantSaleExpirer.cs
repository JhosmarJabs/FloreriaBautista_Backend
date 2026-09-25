using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.InstantSales;
using FloreriaBautista.Services.Interfaces;
using FloreriaBautista.Services.Realtime;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Services.Scheduler;

/// <summary>
/// Regla de escalamiento, expiracion por no-respuesta y liberacion de reserva
/// de solicitudes de venta instantanea. La ejecutan dos disparadores:
/// <see cref="InstantSaleExpirerService"/> (cada 30s, en segundo plano) y el
/// endpoint manual <c>POST /api/admin/orders/expirar-solicitudes-instantaneas</c>.
/// Ambos entran por aqui para que la regla viva en un solo sitio.
///
/// En una sola pasada:
/// 1. Solicitudes PENDIENTE con 2-5 min de edad sin escalar: marca EscaladaAEmpleadoEn.
/// 2. Solicitudes PENDIENTE con mas de 5 min: Estado = EXPIRADA, SIN_RESPUESTA.
/// 3. Solicitudes ACEPTADA con reserva vencida y sin Order: Estado = EXPIRADA, RESERVA_VENCIDA_SIN_PAGO.
/// </summary>
public class InstantSaleExpirer : IInstantSaleExpirer
{
    // -- Timeouts configurables (Pregunta 8 del plan completo) ------
    private static readonly TimeSpan EscalamientoEnMin   = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ExpiracionEnMin     = TimeSpan.FromMinutes(5);

    private readonly AppDbContext                    _context;
    private readonly IFechaHelper                    _fechas;
    private readonly IAuditService                   _audit;
    private readonly IRealtimeNotifier               _realtime;
    private readonly ILogger<InstantSaleExpirer>     _logger;

    public InstantSaleExpirer(
        AppDbContext context,
        IFechaHelper fechas,
        IAuditService audit,
        IRealtimeNotifier realtime,
        ILogger<InstantSaleExpirer> logger)
    {
        _context  = context;
        _fechas   = fechas;
        _audit    = audit;
        _realtime = realtime;
        _logger   = logger;
    }

    public async Task<ExpiracionResultDto> RevisarPendientesAsync(Guid? usuarioId = null)
    {
        var ahora    = _fechas.AhoraUtc();
        var corte2   = ahora - EscalamientoEnMin;   // creadas antes de esto = mas de 2 min
        var corte5   = ahora - ExpiracionEnMin;     // creadas antes de esto = mas de 5 min
        var disparo  = usuarioId.HasValue ? "MANUAL" : "AUTOMATICO";
        var resultado = new ExpiracionResultDto();

        // ── 1. Escalar solicitudes entre 2 y 5 min sin escalamiento ────
        // Solo marca EscaladaAEmpleadoEn si hay un responsable de turno activo.
        var responsable = await _context.Users
            .FirstOrDefaultAsync(u => u.EsResponsableTurno);

        if (responsable != null)
        {
            var escaladas = await _context.SolicitudesVentaInstantanea
                .Where(s => s.Estado == "PENDIENTE"
                         && s.CreadaEn <= corte2
                         && s.CreadaEn > corte5
                         && s.EscaladaAEmpleadoEn == null)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(s => s.EscaladaAEmpleadoEn, ahora));

            resultado.Escaladas = escaladas;

            if (escaladas > 0)
            {
                _logger.LogInformation(
                    "InstantSaleExpirer ({Disparo}): {N} solicitud(es) escalada(s) al responsable de turno {Nombre}.",
                    disparo, escaladas, $"{responsable.Nombre} {responsable.Apellido}".Trim());

                // SignalR: notificar al responsable de turno sobre cada escalada
                var escaladaItems = await _context.SolicitudesVentaInstantanea
                    .Include(s => s.Customer)
                    .Include(s => s.Product)
                    .Where(s => s.Estado == "PENDIENTE"
                             && s.EscaladaAEmpleadoEn == ahora)
                    .ToListAsync();

                foreach (var item in escaladaItems)
                {
                    await _realtime.SolicitudEscaladaAsync(new SolicitudVentaInstantaneaDto
                    {
                        Id                  = item.Id,
                        CustomerId          = item.CustomerId,
                        NombreCliente       = $"{item.Customer.Nombre} {item.Customer.Apellido}".Trim(),
                        TelefonoCliente     = item.Customer.Telefono,
                        ProductId           = item.ProductId,
                        NombreProducto      = item.Product.Nombre,
                        ImagenProducto      = item.Product.ImagenUrl,
                        Cantidad            = item.Cantidad,
                        Estado              = item.Estado,
                        CreadaEn            = item.CreadaEn,
                        EscaladaAEmpleadoEn = item.EscaladaAEmpleadoEn,
                    });
                }
            }
        }
        else
        {
            // Sin responsable: las solicitudes siguen PENDIENTE y visibles
            // para el admin. No se pierden (AC-06).
            var sinEscalar = await _context.SolicitudesVentaInstantanea
                .CountAsync(s => s.Estado == "PENDIENTE"
                              && s.CreadaEn <= corte2
                              && s.CreadaEn > corte5
                              && s.EscaladaAEmpleadoEn == null);

            if (sinEscalar > 0)
            {
                _logger.LogWarning(
                    "InstantSaleExpirer: {N} solicitud(es) pendiente(s) sin responsable de turno asignado.",
                    sinEscalar);
            }
        }

        // ── 2. Expirar por no-respuesta (mas de 5 min PENDIENTE) ───────
        var expiradas = await _context.SolicitudesVentaInstantanea
            .Where(s => s.Estado == "PENDIENTE" && s.CreadaEn <= corte5)
            .ExecuteUpdateAsync(set => set
                .SetProperty(s => s.Estado, "EXPIRADA")
                .SetProperty(s => s.MotivoExpiracion, "SIN_RESPUESTA"));

        resultado.ExpiradasSinRespuesta = expiradas;

        // ── 3. Liberar reservas vencidas sin pago ──────────────────────
        var reservasLiberadas = await _context.SolicitudesVentaInstantanea
            .Where(s => s.Estado == "ACEPTADA"
                     && s.ReservaExpiraEn != null
                     && s.ReservaExpiraEn < ahora
                     && s.OrderId == null)
            .ExecuteUpdateAsync(set => set
                .SetProperty(s => s.Estado, "EXPIRADA")
                .SetProperty(s => s.MotivoExpiracion, "RESERVA_VENCIDA_SIN_PAGO"));

        resultado.ReservasLiberadas = reservasLiberadas;

        // ── Auditoria ──────────────────────────────────────────────────
        if (resultado.Escaladas > 0 || expiradas > 0 || reservasLiberadas > 0)
        {
            await _audit.RegistrarAsync(
                accion:    "EXPIRACION_SOLICITUDES_INSTANTANEAS",
                entidad:   "SolicitudVentaInstantanea",
                entidadId: null,
                usuarioId: usuarioId,
                detalles:  new
                {
                    resultado.Escaladas,
                    resultado.ExpiradasSinRespuesta,
                    resultado.ReservasLiberadas,
                    Disparo = disparo,
                });

            _logger.LogInformation(
                "InstantSaleExpirer ({Disparo}): {Esc} escalada(s), {Exp} expirada(s) por no-respuesta, " +
                "{Lib} reserva(s) liberada(s).",
                disparo, resultado.Escaladas, expiradas, reservasLiberadas);
        }

        return resultado;
    }
}
