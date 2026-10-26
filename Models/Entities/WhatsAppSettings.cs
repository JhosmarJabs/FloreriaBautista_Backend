namespace FloreriaBautista.Models.Entities;

/// <summary>
/// Configuración y estado de la integración de WhatsApp (Evolution API). Fila única (Id = 1),
/// mismo patrón que <see cref="SupplySettings"/>.
/// </summary>
public class WhatsAppSettings
{
    public int       Id                        { get; set; } = 1;
    public bool      Habilitado                { get; set; }

    // ── Conexión (la escribe el monitor) ─────────────────────────
    public string    EstadoConexion            { get; set; } = "DESCONECTADO";
    public string?   NumeroVinculado           { get; set; }
    public DateTime? UltimaVerificacionEn      { get; set; }
    public DateTime? DesconectadoDesde         { get; set; }
    public bool      AvisoDesconexionEnviado   { get; set; }

    // ── Eventos ──────────────────────────────────────────────────
    public bool      NotificarPedidoNuevo      { get; set; } = true;
    public bool      NotificarCambioEstado     { get; set; } = true;
    public string    EstadosNotificados        { get; set; } = "EN_RUTA,ENTREGADO,CANCELADO,PENDIENTE_ANULACION";
    public bool      NotificarStockBajo        { get; set; } = true;
    public bool      NotificarVentaInstantanea { get; set; } = true;
    public bool      NotificarReabastecimiento { get; set; } = true;

    // ── Versión de WhatsApp Web / acción para el cron ────────────
    // SIN USO desde 2026-10-23 (cron retirado, plan notificaciones-whatsapp): estas propiedades y sus
    // columnas se conservan para no desalinear el modelo con el snapshot de EF. Borrarlas sería una
    // migración manual aparte.
    public string?   VersionDeseada            { get; set; }
    public string?   VersionAplicada           { get; set; }
    public string    AccionPendiente           { get; set; } = "NINGUNA";
    public string    EstadoAccion              { get; set; } = "NINGUNA";
    public Guid?     AccionSolicitudId         { get; set; }
    public DateTime? AccionSolicitadaEn        { get; set; }
    public Guid?     AccionSolicitadaPor       { get; set; }
    public DateTime? AccionDesplegadaEn        { get; set; }
    public string?   AccionMensaje             { get; set; }

    public DateTime  ActualizadoEn             { get; set; } = DateTime.UtcNow;
}
