using Microsoft.EntityFrameworkCore;
using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Recommendations;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Services.Interfaces;

namespace FloreriaBautista.Services.Recommendations;

// Propuesta 2 (modelos predictivos) — sistema de recomendación por reglas de asociación
// (market basket, estilo Apriori limitado a pares antecedente→consecuente). Se recalcula
// periódicamente sobre order_items y se guarda en association_rules; ObtenerRecomendadosAsync
// hace fallback a los productos más vendidos cuando no hay regla aplicable (cliente nuevo o
// producto sin historial suficiente).
public class RecommendationService : IRecommendationService
{
    private readonly AppDbContext                    _context;
    private readonly ILogger<RecommendationService>  _logger;

    public RecommendationService(AppDbContext context, ILogger<RecommendationService> logger)
    {
        _context = context;
        _logger  = logger;
    }

    public async Task<RecalcularReglasResultDto> RecalcularReglasAsync()
    {
        var itemsPorOrden = await _context.OrderItems
            .Include(oi => oi.Order)
            .Where(oi => oi.Order.EstadoPedido != "CANCELADO")
            .Select(oi => new { oi.OrderId, oi.ProductId })
            .ToListAsync();

        var canastas = itemsPorOrden
            .GroupBy(x => x.OrderId)
            .Select(g => g.Select(x => x.ProductId).Distinct().ToList())
            .ToList();

        var totalOrdenes = canastas.Count;

        var countAntecedente = new Dictionary<Guid, int>();
        var countPar          = new Dictionary<(Guid Antecedente, Guid Consecuente), int>();

        foreach (var canasta in canastas)
        {
            foreach (var p in canasta)
                countAntecedente[p] = countAntecedente.GetValueOrDefault(p) + 1;

            for (var i = 0; i < canasta.Count; i++)
            for (var j = 0; j < canasta.Count; j++)
            {
                if (i == j) continue;
                var key = (canasta[i], canasta[j]);
                countPar[key] = countPar.GetValueOrDefault(key) + 1;
            }
        }

        var nuevasReglas = new List<AssociationRule>();
        if (totalOrdenes > 0)
        {
            foreach (var ((antecedente, consecuente), coOcurrencias) in countPar)
            {
                var soporte   = (decimal)coOcurrencias / totalOrdenes;
                var confianza = (decimal)coOcurrencias / countAntecedente[antecedente];

                nuevasReglas.Add(new AssociationRule
                {
                    Id                     = Guid.NewGuid(),
                    ProductIdAntecedente   = antecedente,
                    ProductIdConsecuente   = consecuente,
                    Soporte                = Math.Round(soporte, 4),
                    Confianza              = Math.Round(confianza, 4),
                    CalculadoEn            = DateTime.UtcNow
                });
            }
        }

        // Recalculo completo: se reemplazan las reglas anteriores por las nuevas.
        var reglasAnteriores = _context.AssociationRules;
        _context.AssociationRules.RemoveRange(reglasAnteriores);
        if (nuevasReglas.Count > 0)
            await _context.AssociationRules.AddRangeAsync(nuevasReglas);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Reglas de asociación recalculadas: {Reglas} reglas sobre {Ordenes} pedidos.",
            nuevasReglas.Count, totalOrdenes);

        return new RecalcularReglasResultDto
        {
            ReglasGeneradas     = nuevasReglas.Count,
            TransaccionesUsadas = totalOrdenes,
            CalculadoEn         = DateTime.UtcNow
        };
    }

    public async Task<List<ProductRecommendationDto>> ObtenerRecomendadosAsync(IEnumerable<Guid> productosEnContexto, int topN = 4)
    {
        var contexto = productosEnContexto.Distinct().ToList();

        var recomendados = new List<ProductRecommendationDto>();

        if (contexto.Count > 0)
        {
            var reglas = await _context.AssociationRules
                .Where(r => contexto.Contains(r.ProductIdAntecedente) && !contexto.Contains(r.ProductIdConsecuente))
                .Include(r => r.ProductoConsecuente)
                .Where(r => r.ProductoConsecuente.Activo)
                .ToListAsync();

            recomendados = reglas
                .GroupBy(r => r.ProductIdConsecuente)
                .Select(g => g.OrderByDescending(r => r.Confianza).First())
                .OrderByDescending(r => r.Confianza)
                .Take(topN)
                .Select(r => new ProductRecommendationDto
                {
                    ProductId  = r.ProductoConsecuente.Id,
                    Nombre     = r.ProductoConsecuente.Nombre,
                    PrecioBase = r.ProductoConsecuente.PrecioBase,
                    ImagenUrl  = r.ProductoConsecuente.ImagenUrl,
                    Confianza  = r.Confianza,
                    EsFallback = false
                })
                .ToList();
        }

        // Fallback: completar con los productos más vendidos (excluyendo el contexto y lo ya recomendado)
        if (recomendados.Count < topN)
        {
            var excluidos = contexto.Concat(recomendados.Select(r => r.ProductId)).ToList();
            var faltan    = topN - recomendados.Count;

            var masVendidos = await _context.OrderItems
                .Include(oi => oi.Order)
                .Include(oi => oi.Product)
                .Where(oi => oi.Order.EstadoPedido != "CANCELADO" && oi.Product.Activo && !excluidos.Contains(oi.ProductId))
                .GroupBy(oi => oi.ProductId)
                .Select(g => new { ProductId = g.Key, Vendidos = g.Sum(oi => oi.Cantidad) })
                .OrderByDescending(g => g.Vendidos)
                .Take(faltan)
                .ToListAsync();

            var idsMasVendidos = masVendidos.Select(m => m.ProductId).ToList();
            var productos = await _context.Products
                .Where(p => idsMasVendidos.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            foreach (var m in masVendidos)
            {
                if (!productos.TryGetValue(m.ProductId, out var producto)) continue;
                recomendados.Add(new ProductRecommendationDto
                {
                    ProductId  = producto.Id,
                    Nombre     = producto.Nombre,
                    PrecioBase = producto.PrecioBase,
                    ImagenUrl  = producto.ImagenUrl,
                    Confianza  = null,
                    EsFallback = true
                });
            }

            // Último respaldo: si ni siquiera hay ventas históricas (tienda nueva), mostrar
            // productos activos cualesquiera para no dejar la sección vacía.
            if (recomendados.Count < topN)
            {
                var excluidos2 = contexto.Concat(recomendados.Select(r => r.ProductId)).ToList();
                var faltan2    = topN - recomendados.Count;
                var catalogo   = await _context.Products
                    .Where(p => p.Activo && !excluidos2.Contains(p.Id))
                    .OrderByDescending(p => p.CreadoEn)
                    .Take(faltan2)
                    .ToListAsync();

                recomendados.AddRange(catalogo.Select(p => new ProductRecommendationDto
                {
                    ProductId  = p.Id,
                    Nombre     = p.Nombre,
                    PrecioBase = p.PrecioBase,
                    ImagenUrl  = p.ImagenUrl,
                    Confianza  = null,
                    EsFallback = true
                }));
            }
        }

        return recomendados;
    }
}
