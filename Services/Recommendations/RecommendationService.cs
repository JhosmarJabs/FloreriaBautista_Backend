using Microsoft.EntityFrameworkCore;
using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Recommendations;
using FloreriaBautista.Services.Interfaces;

namespace FloreriaBautista.Services.Recommendations;

// ─────────────────────────────────────────────────────────────────────────────────────────
// Solución 2 — Recomendación por filtrado colaborativo ítem-ítem
//
// QUÉ CAMBIÓ Y POR QUÉ
//     La versión anterior calculaba reglas de asociación (soporte/confianza sobre pares de
//     productos) directamente en C#. Se sustituyó por dos razones:
//
//     1. No era el modelo autorizado. La propuesta aprobada es filtrado colaborativo.
//        Además, las reglas de asociación no aplican a este negocio: el 82% de los pedidos
//        trae un solo producto, así que casi no hay coocurrencia dentro de la canasta de
//        donde extraer reglas con soporte útil.
//
//     2. Reimplementar el algoritmo en el backend viola el §13 del proyecto. El modelo que
//        se evalúa en la libreta tiene que ser el mismo que ejecuta el sistema.
//
// CÓMO FUNCIONA AHORA
//     La libreta 02_recomendacion_colaborativa.ipynb calcula la similitud coseno entre
//     productos usando SOLO las compras anteriores al corte temporal, precalcula el top-N
//     por producto y lo exporta a recomendador.json. Este servicio lee ese ranking. No
//     recalcula nada: por construcción no puede divergir de lo que se reportó en la libreta.
//
// RESPALDO
//     Cuando no hay artefacto, el producto no tiene vector (arranque en frío) o faltan
//     sugerencias para completar el top-N, se cae a los más vendidos. Esas recomendaciones
//     se marcan con EsFallback para poder distinguirlas en la interfaz y en las evidencias.
// ─────────────────────────────────────────────────────────────────────────────────────────
public class RecommendationService : IRecommendationService
{
    private readonly AppDbContext                   _context;
    private readonly RecommenderArtifact            _artefacto;
    private readonly ILogger<RecommendationService> _logger;

    private const string MotivoModelo   = "Clientes con gustos parecidos también compraron este producto.";
    private const string MotivoFallback = "Uno de los productos más vendidos de la florería.";

    public RecommendationService(AppDbContext context, RecommenderArtifact artefacto,
                                 ILogger<RecommendationService> logger)
    {
        _context   = context;
        _artefacto = artefacto;
        _logger    = logger;
    }

    public Task<RecomendadorEstadoDto> RecargarArtefactoAsync()
    {
        _artefacto.Cargar();
        return ObtenerEstadoAsync();
    }

    public Task<RecomendadorEstadoDto> ObtenerEstadoAsync()
    {
        var d = _artefacto.Datos;
        return Task.FromResult(new RecomendadorEstadoDto
        {
            Disponible     = _artefacto.Disponible,
            Configuracion  = d?.Configuracion,
            Scoring        = d?.Scoring,
            VersionSklearn = d?.VersionSklearn,
            NProductos     = d?.IndiceProductos.Count ?? 0,
            GeneradoEn     = d?.GeneradoEn,
            CargadoEn      = _artefacto.CargadoEn,
            Error          = _artefacto.ErrorCarga
        });
    }

    public async Task<List<ProductRecommendationDto>> ObtenerRecomendadosAsync(
        IEnumerable<Guid> productosEnContexto, int topN = 4)
    {
        var contexto = productosEnContexto.Distinct().ToList();
        var recomendados = new List<ProductRecommendationDto>();

        // ── 1. Consulta al artefacto ──────────────────────────────────────────────────────
        if (contexto.Count > 0 && _artefacto.Disponible)
        {
            // Cantidad 1 por producto: el carrito no expone cantidades a este servicio, y con
            // peso uniforme la fórmula ponderada se reduce a la suma simple de similitudes.
            var carrito  = contexto.ToDictionary(id => id, _ => 1);
            var puntajes = _artefacto.Puntuar(carrito);

            if (puntajes.Count > 0)
            {
                var ids = puntajes.Select(p => p.ProductId).ToList();
                var activos = await _context.Products
                    .Where(p => ids.Contains(p.Id) && p.Activo)
                    .ToDictionaryAsync(p => p.Id);

                foreach (var (productId, score) in puntajes)
                {
                    if (recomendados.Count >= topN) break;
                    if (!activos.TryGetValue(productId, out var producto)) continue;

                    recomendados.Add(new ProductRecommendationDto
                    {
                        ProductId  = producto.Id,
                        Nombre     = producto.Nombre,
                        PrecioBase = producto.PrecioBase,
                        ImagenUrl  = producto.ImagenUrl,
                        Afinidad   = Math.Round(score, 4),
                        EsFallback = false,
                        Motivo     = MotivoModelo
                    });
                }
            }
        }
        else if (contexto.Count > 0 && !_artefacto.Disponible)
        {
            _logger.LogWarning(
                "El artefacto del recomendador no está disponible ({Error}). Se responde con los más vendidos.",
                _artefacto.ErrorCarga ?? "sin cargar");
        }

        // ── 2. Respaldo: más vendidos según el artefacto ──────────────────────────────────
        if (recomendados.Count < topN)
        {
            var excluidos = contexto.Concat(recomendados.Select(r => r.ProductId)).ToHashSet();
            var candidatos = _artefacto.MasVendidos().Where(id => !excluidos.Contains(id)).ToList();

            if (candidatos.Count > 0)
            {
                var activos = await _context.Products
                    .Where(p => candidatos.Contains(p.Id) && p.Activo)
                    .ToDictionaryAsync(p => p.Id);

                foreach (var id in candidatos)
                {
                    if (recomendados.Count >= topN) break;
                    if (!activos.TryGetValue(id, out var producto)) continue;
                    recomendados.Add(NuevoFallback(producto));
                }
            }
        }

        // ── 3. Respaldo del respaldo: más vendidos calculados sobre la BD ─────────────────
        // Solo se llega aquí si el artefacto no cargó. Es una consulta directa, no un modelo.
        if (recomendados.Count < topN)
        {
            var excluidos = contexto.Concat(recomendados.Select(r => r.ProductId)).ToList();
            var faltan    = topN - recomendados.Count;

            var masVendidos = await _context.OrderItems
                .Where(oi => oi.Order.EstadoPedido == "ENTREGADO"
                          && oi.Product.Activo
                          && !excluidos.Contains(oi.ProductId))
                .GroupBy(oi => oi.ProductId)
                .Select(g => new { ProductId = g.Key, Vendidos = g.Sum(oi => oi.Cantidad) })
                .OrderByDescending(g => g.Vendidos)
                .Take(faltan)
                .ToListAsync();

            var ids = masVendidos.Select(m => m.ProductId).ToList();
            var productos = await _context.Products.Where(p => ids.Contains(p.Id))
                                                   .ToDictionaryAsync(p => p.Id);
            foreach (var m in masVendidos)
                if (productos.TryGetValue(m.ProductId, out var producto))
                    recomendados.Add(NuevoFallback(producto));
        }

        // ── 4. Último recurso: catálogo activo, para no dejar la sección vacía ────────────
        if (recomendados.Count < topN)
        {
            var excluidos = contexto.Concat(recomendados.Select(r => r.ProductId)).ToList();
            var catalogo = await _context.Products
                .Where(p => p.Activo && !excluidos.Contains(p.Id))
                .OrderByDescending(p => p.CreadoEn)
                .Take(topN - recomendados.Count)
                .ToListAsync();

            recomendados.AddRange(catalogo.Select(p => NuevoFallback(p, "Novedad en el catálogo.")));
        }

        return recomendados;
    }

    private static ProductRecommendationDto NuevoFallback(Models.Entities.Product p, string? motivo = null) => new()
    {
        ProductId  = p.Id,
        Nombre     = p.Nombre,
        PrecioBase = p.PrecioBase,
        ImagenUrl  = p.ImagenUrl,
        Afinidad   = null,
        EsFallback = true,
        Motivo     = motivo ?? MotivoFallback
    };
}
