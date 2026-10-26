using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Inventory;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace FloreriaBautista.Services;

public class ForecastService : IForecastService
{
    private readonly AppDbContext          _context;
    private readonly IFechaHelper          _fechas;
    private readonly IAvailabilityService  _availability;
    private readonly IMlPredictionClient   _mlClient;
    private readonly IMemoryCache          _cache;
    private readonly ILogger<ForecastService> _logger;

    private static readonly decimal[] Pesos = [1.0m, 0.9m, 0.8m, 0.7m, 0.55m, 0.4m, 0.3m, 0.2m];

    public ForecastService(
        AppDbContext context, IFechaHelper fechas,
        IAvailabilityService availability, IMlPredictionClient mlClient,
        IMemoryCache cache, ILogger<ForecastService> logger)
    {
        _context      = context;
        _fechas       = fechas;
        _availability = availability;
        _mlClient     = mlClient;
        _cache        = cache;
        _logger       = logger;
    }

    public async Task<ForecastResultDto> GenerarRecomendacionAsync(Guid inventoryItemId)
    {
        var item = await _context.InventoryItems.FindAsync(inventoryItemId)
            ?? throw new NotFoundException("InventoryItem", inventoryItemId);

        var settings = await ObtenerSettingsAsync();

        // ── Semanas de referencia ─────────────────────────────────
        var lunesActual   = LunesDe(DateTime.UtcNow.Date);
        var lunesT        = lunesActual.AddDays(-7);
        var lunesObjetivo = lunesActual;
        var semanaObjetivo = EtiquetaSemana(lunesObjetivo);

        // ── Consumo semanal histórico ─────────────────────────────
        var movimientos = await _context.InventoryMovements
            .Where(m => m.InventoryItemId == inventoryItemId && m.TipoMovimiento == "SALIDA"
                     && m.MotivoCategoria == "VENTA")
            .OrderBy(m => m.FechaHora)
            .Select(m => new { m.FechaHora, m.Cantidad })
            .ToListAsync();

        var consumoPorSemana = movimientos
            .GroupBy(m => LunesDe(m.FechaHora))
            .ToDictionary(g => g.Key, g => (decimal)g.Sum(x => x.Cantidad));

        // Rejilla sin huecos
        var semanasOrdenadas = new List<(DateOnly Lunes, decimal Consumo)>();
        if (consumoPorSemana.Count > 0)
        {
            for (var l = consumoPorSemana.Keys.Min(); l <= lunesT; l = l.AddDays(7))
                semanasOrdenadas.Add((l, consumoPorSemana.GetValueOrDefault(l, 0m)));
        }

        var totalSemanas = semanasOrdenadas.Count;

        // ── SIN_DATOS ─────────────────────────────────────────────
        if (totalSemanas == 0)
        {
            return new ForecastResultDto
            {
                InventoryItemId = item.Id,
                Nombre          = item.Nombre,
                UnidadMedida    = item.UnidadMedida,
                SemanaObjetivo  = semanaObjetivo,
                NivelConfianza  = "SIN_DATOS",
                StockActual     = item.StockActual,
                Explicacion     = "Sin historial de ventas para este insumo. No se genera predicción."
            };
        }

        // ── 1. CONSUMO BASE (media móvil ponderada) ──────────────
        var n = Math.Min(8, totalSemanas);
        var recientes = semanasOrdenadas.TakeLast(n).Select(s => s.Consumo).ToList();
        recientes.Reverse();
        decimal sumaPonderada = 0, sumaPesos = 0;
        for (var i = 0; i < recientes.Count; i++)
        {
            sumaPonderada += recientes[i] * Pesos[i];
            sumaPesos     += Pesos[i];
        }
        var consumoReciente = sumaPonderada / sumaPesos;

        // ── 2. TENDENCIA ─────────────────────────────────────────
        decimal tendencia = 0;
        if (totalSemanas >= 5)
        {
            var ult4 = semanasOrdenadas.TakeLast(4).Average(s => s.Consumo);
            var prev4Count = Math.Min(4, totalSemanas - 4);
            var prev4 = semanasOrdenadas.Skip(totalSemanas - 4 - prev4Count).Take(prev4Count).Average(s => s.Consumo);
            tendencia = ult4 - prev4;
        }

        // ── 3. DEMANDA NO CUBIERTA ───────────────────────────────
        var hace4Semanas = _fechas.InicioDelDiaUtc(lunesT.AddDays(-21));
        var finSemanaT   = _fechas.InicioDelDiaUtc(lunesT.AddDays(7));
        var totalDemandaNoCubierta = await _context.UncoveredDemands
            .Where(d => d.InventoryItemId == inventoryItemId
                     && d.FechaRegistro >= hace4Semanas && d.FechaRegistro < finSemanaT)
            .SumAsync(d => (int?)d.Deficit) ?? 0;
        var demandaExtra = totalDemandaNoCubierta / 4.0m;

        // ── 4. TEMPORADA ─────────────────────────────────────────
        var festividades = await _context.Catalogos
            .Where(c => c.Activo && c.MesDiaInicio != null && c.MesDiaFin != null)
            .Select(c => new { c.Nombre, c.MesDiaInicio, c.MesDiaFin })
            .ToListAsync();

        string? temporadaDetectada = null;
        decimal factorTemporada = 1.0m;
        for (var i = 0; i < 7; i++)
        {
            var mesDia = lunesObjetivo.AddDays(i).ToString("MM-dd");
            var f = festividades.FirstOrDefault(x => EnVentanaMesDia(mesDia, x.MesDiaInicio!, x.MesDiaFin!));
            if (f != null)
            {
                temporadaDetectada = f.Nombre;
                var lunesAnioAnterior = lunesObjetivo.AddDays(-364);
                var consumoAnioAnterior = consumoPorSemana.GetValueOrDefault(lunesAnioAnterior, 0m);
                if (consumoAnioAnterior > 0 && consumoReciente > 0)
                    factorTemporada = consumoAnioAnterior / consumoReciente;
                else
                    factorTemporada = 1.5m;
                break;
            }
        }

        // ── 5. CONSUMO PREDICHO ──────────────────────────────────
        var consumoPredicho = (consumoReciente + tendencia + demandaExtra) * factorTemporada;
        consumoPredicho = Math.Max(0, consumoPredicho);

        // ── 6. MERMA ESPERADA ────────────────────────────────────
        var mermaEsperada = consumoPredicho * (decimal)item.FactorMermaUso;

        // ── 7. PEDIDOS ANTICIPADOS ───────────────────────────────
        var inicioObj = lunesObjetivo.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var finObj    = lunesObjetivo.AddDays(7).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var demandaFutura = await _context.OrderItems
            .Where(oi => oi.Order.TipoPedido == "ANTICIPADO"
                      && oi.Order.EstadoPedido != "CANCELADO"
                      && oi.Order.EstadoPedido != "ENTREGADO"
                      && oi.Order.FechaEntrega >= lunesObjetivo
                      && oi.Order.FechaEntrega < lunesObjetivo.AddDays(7))
            .Join(_context.ProductRecipes.Where(r => r.InventoryItemId == inventoryItemId),
                  oi => oi.ProductId, r => r.ProductId,
                  (oi, r) => oi.Cantidad * r.CantidadRequerida)
            .SumAsync(x => (int?)x) ?? 0;

        // ── 8. CANTIDAD RECOMENDADA ──────────────────────────────
        var necesidad = consumoPredicho + mermaEsperada + demandaFutura;
        var margenSeguridad = necesidad * settings.MargenSeguridadPorcentaje;
        var cantidadRecomendada = (int)Math.Round(necesidad + margenSeguridad - item.StockActual,
                                                   MidpointRounding.AwayFromZero);
        cantidadRecomendada = Math.Max(0, cantidadRecomendada);

        // ── 9. RANGO ─────────────────────────────────────────────
        var consumos = semanasOrdenadas.TakeLast(Math.Min(8, totalSemanas)).Select(s => (double)s.Consumo).ToList();
        var variabilidad = consumos.Count > 1 ? StdDev(consumos) : 0;
        var rangoInf = Math.Max(0, cantidadRecomendada - (int)Math.Round(variabilidad));
        var rangoSup = cantidadRecomendada + (int)Math.Round(variabilidad);

        // ── 10. CONFIANZA ────────────────────────────────────────
        string confianza;
        if (totalSemanas >= 8 && consumoReciente > 0 && (decimal)variabilidad / consumoReciente < 0.3m)
            confianza = "ALTA";
        else if (totalSemanas >= 4)
            confianza = "MEDIA";
        else
            confianza = "BAJA";

        // ── ML comparativo (Paso 18) ─────────────────────────────
        int? prediccionMl = null;
        decimal? diferenciaPct = null;
        if (totalSemanas >= 12)
        {
            try
            {
                decimal ConsumoDe(DateOnly lunes) => consumoPorSemana.GetValueOrDefault(lunes, 0m);
                decimal Promedio(int semanas) => Math.Round(
                    Enumerable.Range(0, semanas).Select(i => ConsumoDe(lunesT.AddDays(-7 * i))).Average(), 2);

                var numPedidos = await _context.OrderItems
                    .Where(oi => oi.Order.EstadoPedido == "ENTREGADO"
                              && oi.Order.FechaCreacion >= inicioObj.AddDays(-7)
                              && oi.Order.FechaCreacion < inicioObj)
                    .Join(_context.ProductRecipes.Where(r => r.InventoryItemId == inventoryItemId),
                          oi => oi.ProductId, r => r.ProductId, (oi, r) => oi.OrderId)
                    .Distinct()
                    .CountAsync();

                var cantT  = ConsumoDe(lunesT);
                var cantT1 = ConsumoDe(lunesT.AddDays(-7));

                var features = new Models.DTOs.Analytics.SupplyFeaturesDto
                {
                    MesObjetivo                 = lunesObjetivo.Month,
                    EsTemporadaAlta             = temporadaDetectada != null ? 1 : 0,
                    CantSemanaActual            = cantT,
                    CantSemanaAnterior          = cantT1,
                    Cant2SemanasAtras           = ConsumoDe(lunesT.AddDays(-14)),
                    PromedioMovil4              = Promedio(4),
                    PromedioMovil8              = Promedio(8),
                    CantMismaSemanaAnioAnterior = ConsumoDe(lunesObjetivo.AddDays(-364)),
                    NumPedidosSemanaActual      = numPedidos,
                    EsFlorPrimaria              = item.EsFlorPrimaria ? 1 : 0,
                    VariacionPctSemanaAnterior  = cantT1 > 0 ? Math.Round((cantT - cantT1) / cantT1, 4) : 0m,
                    TemporadaObjetivo           = temporadaDetectada ?? "SIN_TEMPORADA",
                    UnidadMedida                = string.IsNullOrWhiteSpace(item.UnidadMedida) ? "TALLO" : item.UnidadMedida!
                };

                var mlResult = await _mlClient.PredecirSurtidoAsync(features);
                prediccionMl = (int)Math.Round(Math.Max(0, mlResult.ConsumoPredicho));
                if (consumoPredicho > 0)
                    diferenciaPct = Math.Round(Math.Abs(prediccionMl.Value - consumoPredicho) / consumoPredicho * 100, 1);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ml-service no disponible para comparación del insumo {Id}", inventoryItemId);
            }
        }

        // ── Explicación narrativa ────────────────────────────────
        var unidad = item.UnidadMedida ?? "u";
        var explicacion =
            $"Recomendación: comprar {cantidadRecomendada} {unidad} de {item.Nombre} para {semanaObjetivo}. " +
            $"Consumo promedio reciente ({n} sem): {consumoReciente:F1}. " +
            (tendencia != 0 ? $"Tendencia: {(tendencia > 0 ? "+" : "")}{tendencia:F1} (demanda {(tendencia > 0 ? "creciente" : "decreciente")}). " : "") +
            (demandaExtra > 0 ? $"Demanda no cubierta (promedio semanal): {demandaExtra:F1}. " : "") +
            (temporadaDetectada != null ? $"Temporada detectada: '{temporadaDetectada}' (factor ×{factorTemporada:F2}). " : "") +
            $"Merma esperada: {item.FactorMermaUso:P0} = {mermaEsperada:F1}. " +
            $"Margen de seguridad: {settings.MargenSeguridadPorcentaje:P0} = {margenSeguridad:F1}. " +
            (demandaFutura > 0 ? $"Pedidos anticipados confirmados para esa semana: {demandaFutura}. " : "") +
            $"Stock actual: {item.StockActual}. " +
            $"Necesidad total: {necesidad:F1} → {(int)Math.Round(necesidad + margenSeguridad)}. Menos stock: {cantidadRecomendada}. " +
            $"Rango sugerido: {rangoInf}–{rangoSup} (confianza {confianza}" +
            (consumoReciente > 0 ? $", coeficiente de variación {(decimal)variabilidad / consumoReciente:P0}" : "") +
            ").";

        if (prediccionMl.HasValue && diferenciaPct.HasValue && diferenciaPct > 30)
            explicacion += $" Nota: el modelo ML predice {prediccionMl} {unidad} (diferencia {diferenciaPct:F1}%).";

        return new ForecastResultDto
        {
            InventoryItemId         = item.Id,
            Nombre                  = item.Nombre,
            UnidadMedida            = item.UnidadMedida,
            SemanaObjetivo          = semanaObjetivo,
            ConsumoPredicho         = (int)Math.Round(consumoPredicho),
            CantidadRecomendada     = cantidadRecomendada,
            RangoInferior           = rangoInf,
            RangoSuperior           = rangoSup,
            NivelConfianza          = confianza,
            StockActual             = item.StockActual,
            DemandaFuturaConfirmada = demandaFutura,
            Factores = new ForecastFactorsDto
            {
                ConsumoPromedioReciente  = Math.Round(consumoReciente, 2),
                Tendencia                = Math.Round(tendencia, 2),
                DemandaNoCubierta        = Math.Round(demandaExtra, 2),
                FactorTemporada          = factorTemporada,
                TemporadaDetectada       = temporadaDetectada,
                MermaEsperada            = Math.Round(mermaEsperada, 2),
                MargenSeguridad          = Math.Round(margenSeguridad, 2),
                PedidosAnticipadosSemana = demandaFutura
            },
            Explicacion             = explicacion,
            PrediccionML            = prediccionMl,
            DiferenciaMLvsMathPct   = diferenciaPct
        };
    }

    // ── Helpers ───────────────────────────────────────────────────

    private async Task<Models.Entities.SupplySettings> ObtenerSettingsAsync()
    {
        if (_cache.TryGetValue("supply-settings:v1", out Models.Entities.SupplySettings? cached) && cached is not null)
            return cached;
        var settings = await _context.SupplySettings.FindAsync(1) ?? new Models.Entities.SupplySettings();
        _cache.Set("supply-settings:v1", settings, TimeSpan.FromMinutes(5));
        return settings;
    }

    private static DateOnly LunesDe(DateTime fecha) => DateOnly.FromDateTime(
        System.Globalization.ISOWeek.ToDateTime(
            System.Globalization.ISOWeek.GetYear(fecha),
            System.Globalization.ISOWeek.GetWeekOfYear(fecha),
            DayOfWeek.Monday));

    private static string EtiquetaSemana(DateOnly lunes)
    {
        var d = lunes.ToDateTime(TimeOnly.MinValue);
        return $"{System.Globalization.ISOWeek.GetYear(d)}-W{System.Globalization.ISOWeek.GetWeekOfYear(d):D2}";
    }

    private static bool EnVentanaMesDia(string mesDia, string inicio, string fin)
    {
        if (string.Compare(inicio, fin, StringComparison.Ordinal) <= 0)
            return string.Compare(mesDia, inicio, StringComparison.Ordinal) >= 0
                && string.Compare(mesDia, fin, StringComparison.Ordinal) <= 0;
        return string.Compare(mesDia, inicio, StringComparison.Ordinal) >= 0
            || string.Compare(mesDia, fin, StringComparison.Ordinal) <= 0;
    }

    private static double StdDev(List<double> values)
    {
        var avg = values.Average();
        var sum = values.Sum(v => (v - avg) * (v - avg));
        return Math.Sqrt(sum / values.Count);
    }
}
