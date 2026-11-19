namespace FloreriaBautista.PlayReview;

/// <summary>
/// Configuración del enrutador de borde en PROD. Se activa solo si están definidas
/// <c>REVIEW_UPSTREAM_URL</c> y <c>REVIEW_EDGE_SECRET</c>; si además falta algo
/// imprescindible o es peligroso, queda inactivo y <see cref="MotivoInactivo"/> dice
/// por qué (PROD sigue igual que siempre: nunca se tumba producción por esto).
/// </summary>
public sealed class OpcionesBordeRevision
{
    public bool    Activo         { get; init; }
    public string? MotivoInactivo { get; init; }

    /// <summary>Origen del servicio REVIEW, p. ej. <c>https://floreria-review.onrender.com</c>.</summary>
    public string UpstreamUrl    { get; init; } = string.Empty;
    public string Secreto        { get; init; } = string.Empty;
    /// <summary><c>iss</c> de los JWT que emite REVIEW (su <c>JWT_ISSUER</c>).</summary>
    public string IssuerRevision { get; init; } = string.Empty;
    /// <summary>
    /// Sufijos del correo de las cuentas de revisión (y de los testers de Google Play),
    /// siempre en minúsculas y con <c>@</c>. Sale de <c>REVIEW_LOGIN_EMAIL_DOMAIN</c>,
    /// que acepta varios separados por coma.
    /// </summary>
    public IReadOnlyList<string> DominiosLogin { get; init; } = [PlayReviewEntorno.DominioLoginPorDefecto];

    public static OpcionesBordeRevision Inactivo(string motivo) => new() { Activo = false, MotivoInactivo = motivo };

    public static OpcionesBordeRevision DesdeEntorno() => Desde(Environment.GetEnvironmentVariable);

    public static OpcionesBordeRevision Desde(Func<string, string?> leer)
    {
        if (PlayReviewEntorno.EsValorModoRevision(leer("APP_ENTORNO")))
            return Inactivo("Este proceso es la instancia de revisión: nunca actúa como borde.");

        var upstream = leer("REVIEW_UPSTREAM_URL")?.Trim();
        var secreto  = leer("REVIEW_EDGE_SECRET")?.Trim();

        if (string.IsNullOrEmpty(upstream) || string.IsNullOrEmpty(secreto))
            return Inactivo("Sin REVIEW_UPSTREAM_URL o REVIEW_EDGE_SECRET (comportamiento normal).");

        if (secreto.Length < PlayReviewEntorno.LongitudMinimaSecreto)
            return Inactivo($"REVIEW_EDGE_SECRET debe tener al menos {PlayReviewEntorno.LongitudMinimaSecreto} caracteres.");

        if (!Uri.TryCreate(upstream, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
            return Inactivo("REVIEW_UPSTREAM_URL debe ser una URL https absoluta.");

        if (uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query))
            return Inactivo("REVIEW_UPSTREAM_URL debe ser solo el origen (sin ruta ni query): la firma usa la ruta original.");

        var issuerRevision = leer("REVIEW_JWT_ISSUER")?.Trim();
        if (string.IsNullOrEmpty(issuerRevision))
            return Inactivo("Falta REVIEW_JWT_ISSUER: sin él las peticiones con token de revisión no se reconocerían.");

        // Si fuera igual al de PROD, TODOS los tokens de producción se irían a revisión.
        var issuerProd = leer("JWT_ISSUER")?.Trim();
        if (string.Equals(issuerRevision, issuerProd, StringComparison.Ordinal))
            return Inactivo("REVIEW_JWT_ISSUER no puede ser igual a JWT_ISSUER de producción.");

        var dominios = PlayReviewEntorno.ParsearDominiosLogin(leer("REVIEW_LOGIN_EMAIL_DOMAIN"));
        if (dominios is null)
            return Inactivo("REVIEW_LOGIN_EMAIL_DOMAIN no parece un dominio válido (uno o varios separados por coma).");

        return new OpcionesBordeRevision
        {
            Activo         = true,
            UpstreamUrl    = uri.GetLeftPart(UriPartial.Authority),
            Secreto        = secreto,
            IssuerRevision = issuerRevision,
            DominiosLogin  = dominios,
        };
    }
}

/// <summary>Configuración de la instancia REVIEW (<c>APP_ENTORNO=play_review</c>).</summary>
public sealed class OpcionesModoRevision
{
    public bool   Activo  { get; init; }
    public string Secreto { get; init; } = string.Empty;

    public static OpcionesModoRevision DesdeEntorno(bool esDevelopment) =>
        Desde(Environment.GetEnvironmentVariable, esDevelopment);

    /// <summary>
    /// Si el modo está activo pero mal configurado, lanza: una instancia de revisión
    /// a medio configurar no debe arrancar (prefiere caerse a quedar abierta).
    /// </summary>
    public static OpcionesModoRevision Desde(Func<string, string?> leer, bool esDevelopment)
    {
        if (!PlayReviewEntorno.EsValorModoRevision(leer("APP_ENTORNO")))
            return new OpcionesModoRevision { Activo = false };

        var errores = new List<string>();

        if (esDevelopment)
            errores.Add("ASPNETCORE_ENVIRONMENT=Development no se permite en revisión (activa DevController, Swagger y errores detallados). Usa Production.");

        var secreto = leer("REVIEW_EDGE_SECRET")?.Trim() ?? string.Empty;
        if (secreto.Length < PlayReviewEntorno.LongitudMinimaSecreto)
            errores.Add($"REVIEW_EDGE_SECRET es obligatorio y debe tener al menos {PlayReviewEntorno.LongitudMinimaSecreto} caracteres.");

        var presentes = PlayReviewEntorno.VariablesProhibidasEnRevision
            .Where(v => !string.IsNullOrWhiteSpace(leer(v)))
            .ToList();
        if (presentes.Count > 0)
            errores.Add("Variables de producción presentes en la instancia de revisión (bórralas en Render): "
                        + string.Join(", ", presentes));

        if (errores.Count > 0)
            throw new InvalidOperationException(
                $"{PlayReviewEntorno.PrefijoLog} Configuración inválida:\n - " + string.Join("\n - ", errores));

        return new OpcionesModoRevision { Activo = true, Secreto = secreto };
    }
}
