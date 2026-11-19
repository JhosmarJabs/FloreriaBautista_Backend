namespace FloreriaBautista.PlayReview;

/// <summary>
/// Constantes y lectura del entorno <c>play_review</c> (la instancia aislada que usa
/// Google Play para revisar la app). Ver <c>docs/play-review.md</c>.
///
/// Hay dos papeles y nunca se mezclan en el mismo proceso:
///  - PROD: si tiene <c>REVIEW_UPSTREAM_URL</c> y <c>REVIEW_EDGE_SECRET</c>, el
///    <see cref="PlayReviewEdgeRouter"/> reenvía las peticiones de revisión a REVIEW.
///    Sin esas variables, PROD se comporta exactamente igual que antes.
///  - REVIEW: con <c>APP_ENTORNO=play_review</c> exige la firma del borde, bloquea las
///    operaciones de infraestructura y apaga las integraciones externas.
/// </summary>
public static class PlayReviewEntorno
{
    /// <summary>Valor de <c>APP_ENTORNO</c> que activa el modo de revisión.</summary>
    public const string ValorModoRevision = "play_review";

    /// <summary>Header interno que firma PROD al reenviar. El del cliente siempre se descarta.</summary>
    public const string HeaderFirma = "X-Play-Review-Edge";

    /// <summary>Prefijo de los refresh tokens que emite la instancia de revisión.</summary>
    public const string PrefijoRefresh = "rv_";

    /// <summary>Prefijo de los registros de log del entorno de revisión.</summary>
    public const string PrefijoLog = "[play_review]";

    /// <summary>Dominio de login por defecto de las cuentas de revisión.</summary>
    public const string DominioLoginPorDefecto = "@playreview.invalid";

    /// <summary>Tolerancia de reloj entre PROD y REVIEW para aceptar una firma.</summary>
    public const int ToleranciaFirmaSegundos = 300;

    /// <summary>Longitud mínima del secreto compartido entre PROD y REVIEW.</summary>
    public const int LongitudMinimaSecreto = 32;

    /// <summary>Ruta pública de keep-alive: la única que REVIEW acepta sin firma (solo GET/HEAD).</summary>
    public const string RutaPing = "/api/ping";

    /// <summary>Mensaje de las operaciones bloqueadas en revisión (español e inglés).</summary>
    public const string MensajeBloqueado =
        "Esta función está deshabilitada en el entorno de revisión. / " +
        "This feature is disabled in the review environment.";

    /// <summary>Mensaje cuando una petición llega a REVIEW sin pasar por el borde.</summary>
    public const string MensajeSinFirma =
        "Acceso directo no permitido. / Direct access is not allowed.";

    /// <summary>
    /// Variables que pertenecen solo a producción. Si alguna existe en la instancia de
    /// revisión, la instancia no arranca: es la señal de que se copiaron las variables
    /// de PROD al servicio de revisión. Las <c>DB_*</c> no están aquí porque REVIEW las
    /// necesita (con los datos de su propia base).
    /// </summary>
    public static readonly IReadOnlyList<string> VariablesProhibidasEnRevision =
    [
        "MERCADOPAGO_ACCESS_TOKEN",
        "EVOLUTION_API_URL",
        "EVOLUTION_API_KEY",
        "EVOLUTION_INSTANCE_NAME",
        "WHATSAPP_WEBHOOK_TOKEN",
        "GOOGLE_CLIENT_ID",
        "GOOGLE_CLIENT_SECRET",
        "GOOGLE_DRIVE_FOLDER_ID",
        "GOOGLE_TOKEN_PATH",
        "CLOUDINARY_API_KEY",
        "CLOUDINARY_API_SECRET",
        "ALEXA_API_KEY",
        "BACKUP_DB_USER",
        "BACKUP_DB_PASSWORD",
        "DB_ADMIN_USER",
        "DB_ADMIN_PASSWORD",
        // Una instancia de revisión nunca reenvía a otra (evita bucles).
        "REVIEW_UPSTREAM_URL",
    ];

    /// <summary>¿Este proceso es la instancia de revisión? Se lee en cada llamada (barato).</summary>
    public static bool EsModoRevision => EsValorModoRevision(Environment.GetEnvironmentVariable("APP_ENTORNO"));

    public static bool EsValorModoRevision(string? valor) =>
        string.Equals(valor?.Trim(), ValorModoRevision, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Lee <c>REVIEW_LOGIN_EMAIL_DOMAIN</c>: uno o varios dominios separados por coma
    /// (p. ej. <c>@playreview.invalid,@test.com</c>). Vacío o nulo ⇒ el dominio por defecto.
    /// Cada dominio se normaliza a minúsculas y con <c>@</c> al inicio (así
    /// <c>x@sub.test.com</c> o <c>x@nottest.com</c> nunca coinciden con <c>@test.com</c>).
    /// Devuelve <c>null</c> si alguno no parece un dominio válido: quien llama decide
    /// (el borde queda inactivo; la siembra aborta).
    /// </summary>
    public static IReadOnlyList<string>? ParsearDominiosLogin(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return [DominioLoginPorDefecto];

        var dominios = new List<string>();
        foreach (var parte in valor.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var d = (parte.StartsWith('@') ? parte : "@" + parte).ToLowerInvariant();
            var host = d[1..];
            if (host.Length < 3 || host.IndexOf('.') <= 0 || host.EndsWith('.')
                || host.IndexOf('@') >= 0 || host.Any(char.IsWhiteSpace))
                return null;
            if (!dominios.Contains(d)) dominios.Add(d);
        }

        return dominios.Count == 0 ? [DominioLoginPorDefecto] : dominios;
    }

    /// <summary>¿El correo termina en alguno de los dominios de login de revisión?</summary>
    public static bool CorreoEsDeRevision(string? correo, IReadOnlyList<string> dominios)
    {
        if (string.IsNullOrWhiteSpace(correo)) return false;
        var c = correo.Trim();
        foreach (var d in dominios)
            if (c.EndsWith(d, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
