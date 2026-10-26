using FloreriaBautista.Data;
using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Services.WhatsApp;

public class WhatsAppNotifier : IWhatsAppNotifier
{
    private const int MaxTexto = 1000;

    private readonly IServiceScopeFactory      _scopeFactory;
    private readonly WhatsAppConfig            _config;
    private readonly ILogger<WhatsAppNotifier> _logger;

    public WhatsAppNotifier(IServiceScopeFactory scopeFactory, WhatsAppConfig config,
        ILogger<WhatsAppNotifier> logger)
    {
        _scopeFactory = scopeFactory;
        _config       = config;
        _logger       = logger;
    }

    public async Task<int> EncolarAsync(string tipoEvento, string texto, string? entidadTipo = null,
        Guid? entidadId = null, TimeSpan? deduplicarVentana = null)
    {
        if (!_config.Configurado) return 0;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var s = await db.WhatsAppSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1);
            if (s is null || !s.Habilitado || !EventoActivo(s, tipoEvento)) return 0;

            if (deduplicarVentana.HasValue && entidadId.HasValue)
            {
                var desde = DateTime.UtcNow - deduplicarVentana.Value;
                var duplicado = await db.WhatsAppMessages.AnyAsync(m =>
                    m.TipoEvento == tipoEvento && m.EntidadId == entidadId && m.CreadoEn >= desde);
                if (duplicado) return 0;
            }

            var destinatarios = await db.WhatsAppRecipients
                .Where(r => r.Activo)
                .Select(r => r.Id)
                .ToListAsync();

            return await AgregarAsync(db, destinatarios, tipoEvento, texto, entidadTipo, entidadId);
        }
        catch (Exception ex)
        {
            // Sin el texto: puede llevar datos del cliente.
            _logger.LogError(ex, "No se pudo encolar el aviso de WhatsApp {Tipo} — la operación continúa", tipoEvento);
            return 0;
        }
    }

    public async Task<int> EncolarPruebaAsync(Guid? recipientId)
    {
        if (!_config.Configurado) return 0;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var destinatarios = await db.WhatsAppRecipients
                .Where(r => r.Activo && (recipientId == null || r.Id == recipientId))
                .Select(r => r.Id)
                .ToListAsync();

            return await AgregarAsync(db, destinatarios, WhatsAppConstantes.EventoPrueba,
                "✅ Mensaje de prueba de Florería Bautista. Si lo recibes, los avisos de WhatsApp funcionan.",
                null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo encolar el mensaje de prueba de WhatsApp");
            return 0;
        }
    }

    private static async Task<int> AgregarAsync(AppDbContext db, List<Guid> destinatarios,
        string tipoEvento, string texto, string? entidadTipo, Guid? entidadId)
    {
        if (destinatarios.Count == 0) return 0;

        var ahora = DateTime.UtcNow;
        var cuerpo = texto.Length > MaxTexto ? texto[..MaxTexto] : texto;
        foreach (var id in destinatarios)
        {
            db.WhatsAppMessages.Add(new WhatsAppMessage
            {
                Id          = Guid.NewGuid(),
                RecipientId = id,
                TipoEvento  = tipoEvento,
                EntidadTipo = entidadTipo,
                EntidadId   = entidadId,
                Texto       = cuerpo,
                Estado      = "PENDIENTE",
                CreadoEn    = ahora
            });
        }
        await db.SaveChangesAsync();
        return destinatarios.Count;
    }

    private static bool EventoActivo(WhatsAppSettings s, string tipo) => tipo switch
    {
        WhatsAppConstantes.EventoPedidoNuevo      => s.NotificarPedidoNuevo,
        WhatsAppConstantes.EventoPedidoEstado     => s.NotificarCambioEstado,
        WhatsAppConstantes.EventoStockBajo        => s.NotificarStockBajo,
        WhatsAppConstantes.EventoVentaInstantanea => s.NotificarVentaInstantanea,
        WhatsAppConstantes.EventoReabastecimiento => s.NotificarReabastecimiento,
        _                                         => true
    };
}
