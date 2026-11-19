namespace FloreriaBautista.Services.WhatsApp;

/// <summary>
/// Resultado tipado de una llamada a Evolution. El cliente nunca lanza hacia los callers de negocio:
/// los errores HTTP y los timeouts se convierten aquí.
/// </summary>
public record EvolutionResultado<T>(bool Exito, T? Valor, string? CodigoError, string? Mensaje)
{
    public static EvolutionResultado<T> Ok(T valor) => new(true, valor, null, null);
    public static EvolutionResultado<T> Error(string codigo, string mensaje) => new(false, default, codigo, mensaje);

    /// <summary>Evolution no respondió (caído, red, timeout).</summary>
    public bool Inalcanzable => CodigoError == EvolutionCodigos.Inalcanzable;
}

public static class EvolutionCodigos
{
    public const string NoConfigurado = "NO_CONFIGURADO";
    public const string Inalcanzable  = "INALCANZABLE";
    public const string NoEncontrado  = "NO_ENCONTRADO";
    public const string Http          = "HTTP";
}

public record EvolutionQr(string? Base64, string? PairingCode);

public record EvolutionInstancia(string? Estado, string? Numero, string? Perfil);

public interface IEvolutionClient
{
    /// <summary>Crea la instancia (Baileys, con QR) si todavía no existe.</summary>
    Task<EvolutionResultado<bool>> CrearInstanciaSiNoExisteAsync(CancellationToken ct = default);

    /// <summary>Estado crudo de Evolution: <c>open | connecting | close</c>. NO_ENCONTRADO si no hay instancia.</summary>
    Task<EvolutionResultado<string>> ObtenerEstadoAsync(CancellationToken ct = default);

    /// <summary>Datos de la instancia (número vinculado) desde <c>fetchInstances</c>.</summary>
    Task<EvolutionResultado<EvolutionInstancia>> ObtenerInstanciaAsync(CancellationToken ct = default);

    /// <summary>QR (base64) y código de emparejamiento. Nunca se registran en logs.</summary>
    Task<EvolutionResultado<EvolutionQr>> ConectarAsync(CancellationToken ct = default);

    Task<EvolutionResultado<bool>> DesconectarAsync(CancellationToken ct = default);

    /// <summary>
    /// Reinicia la conexión de la instancia (<c>POST instance/restart/{instancia}</c>); con una sesión
    /// guardada reconecta sin QR. No toca la infraestructura: solo la API de Evolution.
    /// </summary>
    Task<EvolutionResultado<bool>> ReiniciarInstanciaAsync(CancellationToken ct = default);

    Task<EvolutionResultado<bool>> ConfigurarWebhookAsync(string url, string token, CancellationToken ct = default);

    /// <summary>true/false si se pudo verificar; Valor null si no se pudo.</summary>
    Task<EvolutionResultado<bool>> VerificarNumeroAsync(string numero, CancellationToken ct = default);

    Task<EvolutionResultado<bool>> EnviarTextoAsync(string numero, string texto, CancellationToken ct = default);
}
