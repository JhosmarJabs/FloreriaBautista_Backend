namespace FloreriaBautista.Models.Entities;

/// <summary>
/// Vista de una página del sitio público. Es la fuente del reporte de visitas y
/// de la conversión visita → pedido.
///
/// Diseñado SIN PII: no se almacena IP, ni user-agent crudo, ni nada que venga
/// del cliente sin normalizar. La sesión es un identificador anónimo generado en
/// el navegador (sessionStorage) y el referrer se recorta al host, sin ruta ni
/// query string. UsuarioId solo se llena si la petición vino autenticada.
///
/// Retención: el detalle vive 90 días (<see cref="PageVisitDaily"/> conserva el
/// agregado diario de forma permanente). Ver PageVisitRollupService.
/// </summary>
public class PageVisit
{
    public long     Id          { get; set; }

    /// Ruta normalizada del cliente, ej. "/productos/:id". Máx. 200 caracteres.
    public string   Ruta        { get; set; } = string.Empty;

    /// Identificador anónimo de sesión generado en el navegador (no es el userId).
    public string   SesionId    { get; set; } = string.Empty;

    /// Solo si la visita venía con sesión iniciada. Null para visitantes anónimos.
    public Guid?    UsuarioId   { get; set; }

    /// Producto visto, cuando la ruta es un detalle de producto.
    public Guid?    ProductId   { get; set; }

    /// Host del referrer ("google.com"), nunca la URL completa.
    public string?  Referrer    { get; set; }

    /// DESKTOP / MOBILE / TABLET — derivado en el cliente a partir del ancho.
    public string   Dispositivo { get; set; } = "DESKTOP";

    /// Término buscado, cuando el evento es una búsqueda en el sitio.
    public string?  Busqueda    { get; set; }

    /// Número de resultados que devolvió esa búsqueda (0 = búsqueda sin resultado).
    public int?     Resultados  { get; set; }

    public DateTime FechaHora   { get; set; } = DateTime.UtcNow;

    public User?    Usuario { get; set; }
    public Product? Product { get; set; }
}
