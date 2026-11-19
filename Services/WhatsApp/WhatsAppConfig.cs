namespace FloreriaBautista.Services.WhatsApp;

/// <summary>
/// Configuración de la integración, leída una sola vez de las variables de entorno (singleton).
/// Si faltan <c>EVOLUTION_API_URL</c> o <c>EVOLUTION_API_KEY</c> la integración queda deshabilitada:
/// el backend arranca normal y no se encola nada. Ningún valor de aquí se escribe en logs.
/// </summary>
public class WhatsAppConfig
{
    public bool    Configurado  { get; }
    public Uri?    ApiUrl       { get; }
    public string? ApiKey       { get; }
    public string  Instancia    { get; }
    public string? WebhookToken { get; }
    public string? WebhookUrl   { get; }

    // Parámetros de tiempo (constantes por diseño; ver plan §11).
    public static readonly TimeSpan IntervaloMonitoreo   = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan GraciaDesconexion    = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan ExpiracionMensaje    = TimeSpan.FromMinutes(120);
    /// <summary>Tras arrancar el backend, un servicio no disponible cuenta como «conectando» (plan gratuito: Evolution despierta lento).</summary>
    public static readonly TimeSpan GraciaArranque       = TimeSpan.FromMinutes(5);
    /// <summary>Tiempo mínimo entre dos reinicios de la conexión pedidos desde el panel.</summary>
    public static readonly TimeSpan EsperaEntreReinicios = TimeSpan.FromSeconds(60);
    public const int MaxDestinatariosActivos = 5;
    public const int MaxIntentos             = 3;

    public WhatsAppConfig()
    {
        var url = Env("EVOLUTION_API_URL");
        ApiKey  = Env("EVOLUTION_API_KEY");

        if (url is not null && Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            ApiUrl = uri;

        Configurado  = ApiUrl is not null && !string.IsNullOrWhiteSpace(ApiKey);
        Instancia    = Env("EVOLUTION_INSTANCE_NAME") ?? "floreria";
        WebhookToken = Env("WHATSAPP_WEBHOOK_TOKEN");
        WebhookUrl   = Env("WHATSAPP_WEBHOOK_URL_INTERNA");
    }

    private static string? Env(string clave)
    {
        var v = Environment.GetEnvironmentVariable(clave);
        return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }
}

/// <summary>
/// Último estado de conexión conocido, en memoria (singleton). Lo escribe el monitor; el despachador
/// lo lee para no consultar la BD cuando WhatsApp no está conectado.
/// </summary>
public class WhatsAppEstadoActual
{
    private volatile string? _estado;
    private long _ultimoReinicioTicks;

    public string? Estado { get => _estado; set => _estado = value; }

    /// <summary>
    /// true si durante la gracia de arranque se enmascaró un servicio no disponible que venía de CONECTADO:
    /// si la gracia vence sin que Evolution responda, cuenta como caída real. Solo en memoria.
    /// </summary>
    public volatile bool VeniaConectadoAntesDeGracia;

    /// <summary>Hora (UTC) en que arrancó el proceso; se fija al construir el singleton.</summary>
    public DateTime ArrancoEn { get; init; } = DateTime.UtcNow;

    /// <summary>Último reinicio de la conexión pedido desde el panel (solo en memoria, sin BD).</summary>
    public DateTime? UltimoReinicioEn
    {
        get
        {
            var t = Interlocked.Read(ref _ultimoReinicioTicks);
            return t == 0 ? null : new DateTime(t, DateTimeKind.Utc);
        }
        set => Interlocked.Exchange(ref _ultimoReinicioTicks, value?.Ticks ?? 0);
    }
}

/// <summary>Constantes compartidas de la integración.</summary>
public static class WhatsAppConstantes
{
    public const string NoConfigurado        = "NO_CONFIGURADO";
    public const string Conectado            = "CONECTADO";
    public const string Conectando           = "CONECTANDO";
    public const string Desconectado         = "DESCONECTADO";
    public const string ServicioNoDisponible = "SERVICIO_NO_DISPONIBLE";

    public const string EventoPedidoNuevo       = "PEDIDO_NUEVO";
    public const string EventoPedidoEstado      = "PEDIDO_ESTADO";
    public const string EventoStockBajo         = "STOCK_BAJO";
    public const string EventoVentaInstantanea  = "VENTA_INSTANTANEA";
    public const string EventoReabastecimiento  = "REABASTECIMIENTO";
    public const string EventoPrueba            = "PRUEBA";

    /// <summary>52 + 10 u 11 dígitos (mismo CHECK que la BD).</summary>
    public static readonly System.Text.RegularExpressions.Regex RegexTelefono =
        new(@"^52[0-9]{10,11}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    public static readonly System.Text.RegularExpressions.Regex RegexVersion =
        new(@"^\d+\.\d+\.\d+$", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>Número corto de un pedido para los mensajes (8 primeros caracteres del Id).</summary>
    public static string IdCorto(Guid id) => id.ToString("N")[..8].ToUpperInvariant();

    /// <summary>Estado de pedido en texto legible para los mensajes.</summary>
    public static string EstadoLegible(string estado) => estado.ToUpperInvariant() switch
    {
        "PENDIENTE_VALIDACION" => "Pendiente de validación",
        "EN_PREPARACION"       => "En preparación",
        "EN_RUTA"              => "En ruta",
        "PENDIENTE_ANULACION"  => "Pendiente de anulación",
        "ENTREGADO"            => "Entregado",
        "CANCELADO"            => "Cancelado",
        "NO_COMPLETADO"        => "No completado",
        _                      => estado
    };

    /// <summary>Teléfono para listados: <c>52•••••1234</c>.</summary>
    public static string Enmascarar(string telefono) =>
        telefono.Length <= 6 ? "•••••" : $"{telefono[..2]}•••••{telefono[^4..]}";
}
