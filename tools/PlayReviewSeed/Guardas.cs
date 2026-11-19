using Npgsql;

namespace FloreriaBautista.PlayReviewSeed;

/// <summary>Lo que se sabe de la base de destino antes de escribir nada.</summary>
public sealed record EstadoDestino(
    int  TablasEnPublic,
    bool TablaUsuariosExiste,
    long FilasUsuarios,
    long FilasClientes,
    long FilasPedidos,
    bool MarcadorExiste,
    bool MarcadorEsRevision)
{
    public static EstadoDestino Vacia => new(0, false, 0, 0, 0, false, false);
    public bool TieneDatos => FilasUsuarios > 0 || FilasClientes > 0 || FilasPedidos > 0;
}

/// <summary>
/// Guardas de la herramienta de siembra. Son funciones puras (sin red ni base) para
/// poder probarlas; <see cref="Programa"/> las aplica en orden y aborta con la primera
/// que falle, siempre ANTES de escribir.
/// </summary>
public static class Guardas
{
    public const string Marcador = "play_review";

    /// <summary>Guarda 1: la conexión solo sale de REVIEW_DB_URL.</summary>
    public static NpgsqlConnectionStringBuilder ConexionDesdeVariable(string? reviewDbUrl)
    {
        if (string.IsNullOrWhiteSpace(reviewDbUrl))
            throw new GuardaException(
                "REVIEW_DB_URL no está definida. La herramienta no lee .env, DB_* ni DefaultConnection: " +
                "define REVIEW_DB_URL con la cadena de la base de REVISIÓN.");

        return ConexionRevision.Parsear(reviewDbUrl);
    }

    /// <summary>
    /// Guarda 2: si PROD_DB_HOST existe y es el mismo host que el destino, aborta antes
    /// de conectarse. Se compara sin el sufijo <c>-pooler</c> de Neon, porque el host con
    /// pooler y el directo son el mismo endpoint.
    /// </summary>
    public static void HostDistintoDeProduccion(string hostDestino, string? prodDbHost)
    {
        if (string.IsNullOrWhiteSpace(prodDbHost)) return;
        if (string.Equals(NormalizarHost(hostDestino), NormalizarHost(prodDbHost), StringComparison.OrdinalIgnoreCase))
            throw new GuardaException(
                $"El host de REVIEW_DB_URL ({hostDestino}) es el host de PRODUCCIÓN (PROD_DB_HOST). Abortado sin conectarse.");
    }

    /// <summary>
    /// Guarda 3: una base con usuarios, clientes o pedidos y SIN el marcador
    /// <c>_entorno = 'play_review'</c> es producción o una rama copiada con datos reales.
    /// Con marcador y datos, solo se continúa con <c>--reiniciar</c>.
    /// </summary>
    public static void EstadoPermiteSembrar(EstadoDestino estado, bool reiniciar)
    {
        if (estado.MarcadorExiste && !estado.MarcadorEsRevision)
            throw new GuardaException(
                "La tabla _entorno existe pero NO dice 'play_review'. Abortado: esta base no es de revisión.");

        if (estado.TieneDatos && !estado.MarcadorEsRevision)
            throw new GuardaException(
                $"La base tiene datos (usuarios: {estado.FilasUsuarios}, clientes: {estado.FilasClientes}, " +
                $"pedidos: {estado.FilasPedidos}) y no tiene el marcador _entorno='play_review'. " +
                "Parece producción o una rama de Neon copiada CON datos. Abortado. " +
                "Crea la rama como 'schema-only' (o una base vacía).");

        if (estado.TieneDatos && estado.MarcadorEsRevision && !reiniciar)
            throw new GuardaException(
                "La base de revisión ya tiene datos. Usa --reiniciar para borrarlos y volver a sembrar.");
    }

    /// <summary>Guarda 4: confirmar escribiendo el nombre exacto de la base.</summary>
    public static void ConfirmacionCoincide(string nombreBase, string? confirmacion)
    {
        if (!string.Equals(nombreBase, confirmacion?.Trim(), StringComparison.Ordinal))
            throw new GuardaException(
                $"Confirmación incorrecta: se esperaba exactamente '{nombreBase}'. No se escribió nada.");
    }

    public static string NormalizarHost(string host)
    {
        var h = host.Trim().TrimEnd('.').ToLowerInvariant();
        var punto = h.IndexOf('.');
        var primera = punto < 0 ? h : h[..punto];
        var resto   = punto < 0 ? string.Empty : h[punto..];
        if (primera.EndsWith("-pooler", StringComparison.Ordinal)) primera = primera[..^"-pooler".Length];
        return primera + resto;
    }
}

public sealed class GuardaException : Exception
{
    public GuardaException(string mensaje) : base(mensaje) { }
}

/// <summary>Convierte REVIEW_DB_URL (URL <c>postgresql://</c> de Neon o cadena Npgsql) en un builder.</summary>
public static class ConexionRevision
{
    public static NpgsqlConnectionStringBuilder Parsear(string valor)
    {
        valor = valor.Trim();
        NpgsqlConnectionStringBuilder b;

        if (valor.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            || valor.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(valor, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
                throw new GuardaException("REVIEW_DB_URL no es una URL de Postgres válida.");

            var usuario = uri.UserInfo.Split(':', 2);
            b = new NpgsqlConnectionStringBuilder
            {
                Host     = uri.Host,
                Port     = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
                Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
                Username = Uri.UnescapeDataString(usuario[0]),
                Password = usuario.Length > 1 ? Uri.UnescapeDataString(usuario[1]) : null,
                SslMode  = SslMode.Require,
            };

            foreach (var par in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = par.Split('=', 2);
                if (kv.Length == 2 && kv[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase)
                    && Enum.TryParse<SslMode>(kv[1].Replace("-", ""), true, out var modo))
                    b.SslMode = modo;
            }
        }
        else
        {
            try { b = new NpgsqlConnectionStringBuilder(valor); }
            catch (ArgumentException) { throw new GuardaException("REVIEW_DB_URL no es una cadena de conexión válida."); }
        }

        if (string.IsNullOrWhiteSpace(b.Host) || string.IsNullOrWhiteSpace(b.Database))
            throw new GuardaException("REVIEW_DB_URL debe indicar host y nombre de base.");

        b.ApplicationName = "PlayReviewSeed";
        b.CommandTimeout  = 120;
        return b;
    }
}
