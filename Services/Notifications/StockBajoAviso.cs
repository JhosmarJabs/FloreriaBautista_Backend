using FloreriaBautista.Models.Entities;
using FloreriaBautista.Services.WhatsApp;

namespace FloreriaBautista.Services.Notifications;

/// <summary>
/// Regla única del aviso STOCK_BAJO: solo en la transición de por-encima a por-debajo
/// del mínimo, y suprimido si ya existe una notificación no leída reciente (24 h).
/// La usan el movimiento manual de inventario y las salidas por venta de OrderService.
/// Nunca lanza: un fallo al notificar no debe romper la operación que ya se guardó.
/// </summary>
internal static class StockBajoAviso
{
    public static async Task NotificarSiCruzoMinimoAsync(
        InventoryItem item,
        int stockAntes,
        INotificationService notificaciones,
        IWhatsAppNotifier whatsApp,
        ILogger logger)
    {
        if (!(item.StockMinimo > 0
            && stockAntes > item.StockMinimo
            && item.StockActual <= item.StockMinimo))
            return;

        try
        {
            var yaNotificado = await notificaciones.ExisteNoLeidaRecienteAsync(
                "STOCK_BAJO", item.Id, TimeSpan.FromHours(24));

            if (!yaNotificado)
            {
                await notificaciones.CrearParaUsuariosAsync(
                    "STOCK_BAJO",
                    $"Stock bajo: {item.Nombre}",
                    $"{item.Nombre} bajó a {item.StockActual} {item.UnidadMedida ?? "u"} (mínimo: {item.StockMinimo}).",
                    ["ADMIN", "EMPLEADO"],
                    "InventoryItem", item.Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al notificar STOCK_BAJO para insumo {Id}", item.Id);
        }

        // WhatsApp: independiente de `yaNotificado` (que depende de "no leída"); se deduplica
        // por su cuenta con una ventana de 24 h sobre la cola.
        try
        {
            await whatsApp.EncolarAsync(WhatsAppConstantes.EventoStockBajo,
                $"⚠️ Stock bajo: {item.Nombre} quedó en {item.StockActual} {item.UnidadMedida ?? "u"} (mínimo {item.StockMinimo}).",
                "InventoryItem", item.Id, deduplicarVentana: TimeSpan.FromHours(24));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al encolar WhatsApp de STOCK_BAJO para insumo {Id}", item.Id);
        }
    }
}
