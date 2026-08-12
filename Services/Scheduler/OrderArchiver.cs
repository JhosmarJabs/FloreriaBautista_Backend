using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Orders;
using FloreriaBautista.Models.Enums;
using FloreriaBautista.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Services.Scheduler;

/// <summary>
/// Regla de archivo automático de pedidos. La ejecutan dos disparadores:
/// <see cref="OrderArchiverService"/> (cada hora, en segundo plano) y el endpoint
/// <c>POST /api/admin/orders/archivar-atrasados</c> (manual, para no esperar la
/// siguiente ventana). Ambos entran por aquí para que la regla viva en un solo
/// sitio.
///
/// Qué hace con un pedido cuya FechaEntrega ya pasó:
/// <list type="bullet">
///   <item>ENTREGADO / CANCELADO → solo se mueve al archivo.</item>
///   <item>PENDIENTE_VALIDACION / EN_PREPARACION / PENDIENTE_ANULACION → se
///         reescriben a NO_COMPLETADO: nadie les dio seguimiento.</item>
///   <item>EN_RUTA → se archiva CONSERVANDO su estado. Un repartidor puede seguir
///         entregando a las 23:00 y marcarlo NO_COMPLETADO sería mentir sobre lo
///         que pasó. Quedan visibles en la vista "requieren cierre"
///         (<c>GET /api/admin/orders?requierenCierre=true</c>) para que alguien
///         los cierre a mano.</item>
/// </list>
/// Cada transición automática queda en AuditService con Accion =
/// "ARCHIVADO_AUTOMATICO".
/// </summary>
public class OrderArchiver : IOrderArchiver
{
    private readonly AppDbContext            _context;
    private readonly IFechaHelper            _fechas;
    private readonly IAuditService           _audit;
    private readonly ILogger<OrderArchiver>  _logger;

    public OrderArchiver(
        AppDbContext context,
        IFechaHelper fechas,
        IAuditService audit,
        ILogger<OrderArchiver> logger)
    {
        _context = context;
        _fechas  = fechas;
        _audit   = audit;
        _logger  = logger;
    }

    public async Task<ArchivadoResultDto> ArchivarAtrasadosAsync(Guid? usuarioId = null)
    {
        // "Hoy" SIEMPRE en hora de la tienda: con UtcNow, entre las 18:00 y las
        // 23:59 locales el día UTC ya avanzó y se archivarían los pedidos que se
        // entregan hoy mismo.
        var hoy = _fechas.HoyLocal();

        var atrasados = await _context.Orders
            .Where(o => !o.Archivado && o.FechaEntrega < hoy)
            .ToListAsync();

        var resultado = new ArchivadoResultDto { Fecha = hoy, Total = atrasados.Count };
        if (atrasados.Count == 0) return resultado;

        var ahora       = DateTime.UtcNow;
        var transiciones = new List<(Guid Id, string EstadoAnterior, string EstadoNuevo, DateOnly FechaEntrega)>();

        foreach (var pedido in atrasados)
        {
            var estadoAnterior = pedido.EstadoPedido;
            var estado         = estadoAnterior.ToUpperInvariant();

            if (EstadosPedido.Abandonables.Contains(estado))
            {
                pedido.EstadoPedido = EstadosPedido.NoCompletado;
                resultado.NoCompletados++;
            }
            else if (EstadosPedido.Finales.Contains(estado))
            {
                resultado.YaCerrados++;
            }
            else
            {
                // EN_RUTA (o cualquier estado futuro no clasificado): se conserva.
                resultado.RequierenCierre++;
            }

            pedido.Archivado   = true;
            pedido.ArchivadoEn = ahora;

            transiciones.Add((pedido.Id, estadoAnterior, pedido.EstadoPedido, pedido.FechaEntrega));
        }

        await _context.SaveChangesAsync();

        // La auditoría se registra DESPUÉS de guardar: AuditService comparte el
        // mismo DbContext y su SaveChanges arrastraría los pedidos a medio ajustar.
        foreach (var t in transiciones)
        {
            await _audit.RegistrarAsync(
                accion:    "ARCHIVADO_AUTOMATICO",
                entidad:   "Order",
                entidadId: t.Id.ToString(),
                usuarioId: usuarioId,
                detalles:  new
                {
                    EstadoAnterior = t.EstadoAnterior,
                    EstadoNuevo    = t.EstadoNuevo,
                    FechaEntrega   = t.FechaEntrega.ToString("yyyy-MM-dd"),
                    HoyLocal       = hoy.ToString("yyyy-MM-dd"),
                    ZonaHoraria    = _fechas.Zona.Id,
                    Disparo        = usuarioId.HasValue ? "MANUAL" : "AUTOMATICO"
                });
        }

        _logger.LogInformation(
            "Archivado automático ({Disparo}): {Total} pedido(s) atrasado(s) — {NoCompletados} NO_COMPLETADO, " +
            "{RequierenCierre} conservaron estado (requieren cierre), {YaCerrados} ya cerrados. Hoy local: {Hoy}.",
            usuarioId.HasValue ? "manual" : "scheduler",
            resultado.Total, resultado.NoCompletados, resultado.RequierenCierre, resultado.YaCerrados, hoy);

        return resultado;
    }
}
