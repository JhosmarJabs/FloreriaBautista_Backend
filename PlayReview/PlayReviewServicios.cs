using FloreriaBautista.Services.WhatsApp;

namespace FloreriaBautista.PlayReview;

/// <summary>
/// REVIEW: WhatsApp apagado. No encola nada (ni siquiera en la base de revisión), así
/// que el despachador no tendría qué mandar aunque estuviera registrado.
/// </summary>
public class WhatsAppNotifierRevision : IWhatsAppNotifier
{
    private readonly ILogger<WhatsAppNotifierRevision> _logger;
    public WhatsAppNotifierRevision(ILogger<WhatsAppNotifierRevision> logger) => _logger = logger;

    public Task<int> EncolarAsync(string tipoEvento, string texto, string? entidadTipo = null,
        Guid? entidadId = null, TimeSpan? deduplicarVentana = null)
    {
        _logger.LogInformation("{Prefijo} WhatsApp omitido ({Tipo})", PlayReviewEntorno.PrefijoLog, tipoEvento);
        return Task.FromResult(0);
    }

    public Task<int> EncolarPruebaAsync(Guid? recipientId) => Task.FromResult(0);
}

public static class PlayReviewServiceExtensions
{
    /// <summary>
    /// Registra lo que necesita el papel de este proceso: el borde (PROD con variables)
    /// o el modo de revisión (REVIEW). Devuelve el modo para que Program arme el pipeline.
    /// </summary>
    public static (OpcionesBordeRevision Borde, OpcionesModoRevision Revision) AddPlayReview(
        this IServiceCollection services, IWebHostEnvironment env)
    {
        var revision = OpcionesModoRevision.DesdeEntorno(env.IsDevelopment());
        var borde    = revision.Activo
            ? OpcionesBordeRevision.Inactivo("Instancia de revisión.")
            : OpcionesBordeRevision.DesdeEntorno();

        services.AddSingleton(revision);
        services.AddSingleton(borde);

        if (borde.Activo)
        {
            services.AddHttpForwarder();
            services.AddSingleton<ClienteBordeRevision>();
        }

        return (borde, revision);
    }

    public static bool HayVariablesDeBorde() =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("REVIEW_UPSTREAM_URL"))
        || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("REVIEW_EDGE_SECRET"));
}
