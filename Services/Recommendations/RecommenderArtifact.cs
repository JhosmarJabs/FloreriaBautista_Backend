using System.Text.Json;
using System.Text.Json.Serialization;

namespace FloreriaBautista.Services.Recommendations;

// Artefacto de la Solución 2: la matriz de similitud ítem-ítem producida por la libreta
// 06_Notebooks/02_recomendacion_colaborativa.ipynb.
//
// A diferencia de la Solución 1, aquí NO hace falta un sidecar de Python: el modelo ES la
// matriz de similitud, y puntuar es una búsqueda más un ordenamiento. El §5.6 del proyecto
// admite exactamente este tipo de artefacto: "vectorizador, matriz usuario-ítem, índices,
// similitudes o parámetros de un recomendador".
//
// La libreta precalcula el top-N por producto, así que el backend no recalcula nada: lee el
// mismo ranking que se evaluó en la libreta. Eso elimina la posibilidad de que C# produzca
// un resultado distinto al reportado.
//
// Singleton: se carga una vez al arrancar y se mantiene en memoria (son ~60 KB).
public class RecommenderArtifact
{
    private readonly ILogger<RecommenderArtifact> _logger;
    private readonly string _ruta;
    private readonly object _candado = new();

    public ArtefactoRecomendador? Datos { get; private set; }
    public string? ErrorCarga { get; private set; }
    public DateTime? CargadoEn { get; private set; }

    public bool Disponible => Datos is not null && Datos.TopNPorProducto.Count > 0;

    public RecommenderArtifact(ILogger<RecommenderArtifact> logger, IConfiguration config)
    {
        _logger = logger;
        _ruta = Environment.GetEnvironmentVariable("RECOMMENDER_ARTIFACT_PATH")
                ?? config["Recomendador:RutaArtefacto"]
                ?? "/modelos/recomendador.json";
        Cargar();
    }

    // Recarga desde disco. Se invoca al arrancar y cuando el scheduler o un admin lo pide,
    // para que un artefacto regenerado entre en vigor sin reiniciar el contenedor.
    public bool Cargar()
    {
        lock (_candado)
        {
            try
            {
                if (!File.Exists(_ruta))
                    throw new FileNotFoundException($"No se encontró el artefacto en {_ruta}");

                var json = File.ReadAllText(_ruta);
                var datos = JsonSerializer.Deserialize<ArtefactoRecomendador>(json)
                            ?? throw new InvalidOperationException("El artefacto está vacío o mal formado.");

                Datos      = datos;
                ErrorCarga = null;
                CargadoEn  = DateTime.UtcNow;

                _logger.LogInformation(
                    "Recomendador cargado desde {Ruta}: {Productos} productos, configuración '{Config}', generado {Generado}.",
                    _ruta, datos.IndiceProductos.Count, datos.Configuracion, datos.GeneradoEn);
                return true;
            }
            catch (Exception ex)
            {
                ErrorCarga = $"{ex.GetType().Name}: {ex.Message}";
                _logger.LogError(ex,
                    "No se pudo cargar el artefacto del recomendador desde {Ruta}. " +
                    "Las recomendaciones caerán en el respaldo de más vendidos.", _ruta);
                return false;
            }
        }
    }

    // Puntúa productos candidatos a partir de lo que hay en el carrito.
    // Réplica exacta de recomendar_desde_artefacto() de la libreta: suma de similitudes
    // ponderada por la cantidad, excluyendo lo que ya está en el carrito.
    public List<(Guid ProductId, decimal Score)> Puntuar(IDictionary<Guid, int> carrito)
    {
        var datos = Datos;
        if (datos is null || carrito.Count == 0) return [];

        var ponderar = datos.Scoring.StartsWith("ponderado", StringComparison.OrdinalIgnoreCase);
        var puntajes = new Dictionary<Guid, decimal>();

        foreach (var (pid, cantidad) in carrito)
        {
            if (!datos.TopNPorProducto.TryGetValue(pid.ToString(), out var similares)) continue;
            var peso = ponderar ? Math.Max(1, cantidad) : 1;

            foreach (var par in similares)
            {
                if (par.Count < 2) continue;
                if (!Guid.TryParse(par[0].GetString(), out var qid)) continue;
                if (carrito.ContainsKey(qid)) continue;

                var sim = par[1].GetDecimal();
                puntajes[qid] = puntajes.GetValueOrDefault(qid) + (peso * sim);
            }
        }

        return puntajes.OrderByDescending(p => p.Value)
                       .Select(p => (p.Key, p.Value))
                       .ToList();
    }

    public List<Guid> MasVendidos()
    {
        var datos = Datos;
        if (datos is null) return [];
        return datos.FallbackMasVendidos
                    .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
                    .Where(g => g != Guid.Empty)
                    .ToList();
    }
}

// Mapeo del JSON que escribe la libreta. Los nombres siguen el snake_case del artefacto.
public class ArtefactoRecomendador
{
    [JsonPropertyName("solucion")]           public string Solucion { get; set; } = string.Empty;
    [JsonPropertyName("configuracion")]      public string Configuracion { get; set; } = string.Empty;
    [JsonPropertyName("matriz")]             public string Matriz { get; set; } = string.Empty;
    [JsonPropertyName("scoring")]            public string Scoring { get; set; } = "suma simple";
    [JsonPropertyName("version_sklearn")]    public string VersionSklearn { get; set; } = string.Empty;
    [JsonPropertyName("semilla")]            public int    Semilla { get; set; }
    [JsonPropertyName("generado_en")]        public string GeneradoEn { get; set; } = string.Empty;
    [JsonPropertyName("corte_temporal")]     public string CorteTemporal { get; set; } = string.Empty;
    [JsonPropertyName("top_n_recomendaciones")] public int TopNRecomendaciones { get; set; } = 4;
    [JsonPropertyName("n_productos")]        public int    NProductos { get; set; }
    [JsonPropertyName("indice_productos")]   public List<string> IndiceProductos { get; set; } = [];

    // { product_id: [[product_id_similar, similitud], ...] }
    [JsonPropertyName("top_n_por_producto")]
    public Dictionary<string, List<List<JsonElement>>> TopNPorProducto { get; set; } = [];

    [JsonPropertyName("fallback_mas_vendidos")] public List<string> FallbackMasVendidos { get; set; } = [];
}
