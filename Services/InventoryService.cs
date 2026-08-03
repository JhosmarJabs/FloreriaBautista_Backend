using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Analytics;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Inventory;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Services.Interfaces;

namespace FloreriaBautista.Services;

public class InventoryService : IInventoryService
{
    private readonly AppDbContext              _context;
    private readonly ILogger<InventoryService> _logger;
    private readonly IMlPredictionClient       _mlClient;
    private readonly IMemoryCache              _cache;

    private static readonly Regex _nonAlphaNumRegex = new(@"[^a-z0-9\s]", RegexOptions.Compiled);

    // La lista de reabastecimiento ejecuta el modelo por insumo (~decenas de predicciones),
    // así que se cachea: se calcula una vez y las visitas siguientes la leen al instante.
    private const string CacheReabastecimiento = "reabastecimiento:v1";
    private static readonly TimeSpan TtlReabastecimiento = TimeSpan.FromHours(6);

    public InventoryService(AppDbContext context, ILogger<InventoryService> logger,
                            IMlPredictionClient mlClient, IMemoryCache cache)
    {
        _context  = context;
        _logger   = logger;
        _mlClient = mlClient;
        _cache    = cache;
    }

    // ── Listar ────────────────────────────────────────────────────
    public async Task<PagedResultDto<InventoryItemDto>> ListarAsync(
        string? sucursal, bool? bajoMinimo, string? busqueda, int page, int size)
    {
        // FIX: Validación de paginación para evitar divisiones por cero o valores inválidos (Reliability C)
        if (page <= 0) page = 1;
        if (size <= 0) size = 10;
        if (size > 100) size = 100;

        var query = _context.InventoryItems
            .Where(i => i.Activo)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(sucursal))
            query = query.Where(i => i.Sucursal.ToLower() == sucursal.ToLower());

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var b = busqueda.ToLower().Trim();
            query = query.Where(i => i.Nombre.ToLower().Contains(b));
        }

        // FIX: Se movió el filtro de 'bajoMinimo' antes de ejecutar la consulta para filtrar en BD (Performance/Reliability)
        if (bajoMinimo == true)
        {
            query = query.Where(i => i.StockActual <= i.StockMinimo);
        }

        var total = await query.CountAsync();
        var paginado = await query
            .OrderBy(i => i.Nombre)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(i => new InventoryItemDto
            {
                Id           = i.Id,
                Nombre       = i.Nombre,
                StockActual  = i.StockActual,
                StockMinimo  = i.StockMinimo,
                Sucursal     = i.Sucursal,
                SumaAlCosto  = i.SumaAlCosto,
                UnidadMedida = i.UnidadMedida,
                PrecioCosto  = i.PrecioCosto,
                EsFlorPrimaria = i.EsFlorPrimaria,
                ImagenUrl    = i.ImagenUrl,
                Activo       = i.Activo
            })
            .ToListAsync();

        return new PagedResultDto<InventoryItemDto>
        {
            Items        = paginado,
            Total        = total,
            Pagina       = page,
            TamanoPagina = size,
            TotalPaginas = (int)Math.Ceiling(total / (double)size)
        };
    }

    // ── Detalle ───────────────────────────────────────────────────
    public async Task<InventoryItemDto> ObtenerAsync(Guid id)
    {
        var item = await _context.InventoryItems.FindAsync(id)
            ?? throw new NotFoundException("InventoryItem", id);
        return MapToDto(item);
    }

    // ── Crear ─────────────────────────────────────────────────────
    public async Task<InventoryItemDto> CrearAsync(CreateInventoryItemDto request)
    {
        var item = new InventoryItem
        {
            Id           = Guid.NewGuid(),
            Nombre       = request.Nombre.Trim(),
            StockActual  = request.StockActual,
            StockMinimo  = request.StockMinimo,
            Sucursal     = request.Sucursal.Trim().ToUpper(),
            SumaAlCosto  = request.SumaAlCosto,
            UnidadMedida = request.UnidadMedida?.Trim().ToUpper(),
            PrecioCosto  = request.PrecioCosto,
            EsFlorPrimaria = request.EsFlorPrimaria,
            ImagenUrl    = request.ImagenUrl?.Trim(),
            Activo       = true
        };

        _context.InventoryItems.Add(item);
        await _context.SaveChangesAsync();
        _logger.LogInformation("InventoryItem creado: {Nombre} ({Id})", item.Nombre, item.Id);
        return MapToDto(item);
    }

    // ── Actualizar ────────────────────────────────────────────────
    public async Task<InventoryItemDto> ActualizarAsync(Guid id, UpdateInventoryItemDto request)
    {
        var item = await _context.InventoryItems.FindAsync(id)
            ?? throw new NotFoundException("InventoryItem", id);

        if (!string.IsNullOrWhiteSpace(request.Nombre))      item.Nombre      = request.Nombre.Trim();
        if (request.StockActual.HasValue)                     item.StockActual = request.StockActual.Value;
        if (request.StockMinimo.HasValue)                     item.StockMinimo = request.StockMinimo.Value;
        if (request.PrecioCosto.HasValue)                     item.PrecioCosto = request.PrecioCosto.Value;
        if (request.EsFlorPrimaria.HasValue)                 item.EsFlorPrimaria = request.EsFlorPrimaria.Value;
        if (!string.IsNullOrWhiteSpace(request.Sucursal))    item.Sucursal    = request.Sucursal.Trim().ToUpper();
        if (request.SumaAlCosto.HasValue)                     item.SumaAlCosto = request.SumaAlCosto.Value;
        
        if (request.UnidadMedida != null)
            item.UnidadMedida = string.IsNullOrWhiteSpace(request.UnidadMedida)
                ? null : request.UnidadMedida.Trim().ToUpper();

        if (request.ImagenUrl != null)
            item.ImagenUrl = string.IsNullOrWhiteSpace(request.ImagenUrl)
                ? null : request.ImagenUrl.Trim();

        if (request.Activo.HasValue) item.Activo = request.Activo.Value;

        await _context.SaveChangesAsync();
        _logger.LogInformation("InventoryItem actualizado: {Id}", id);
        return MapToDto(item);
    }

    public async Task EliminarAsync(Guid id)
    {
        var item = await _context.InventoryItems.FindAsync(id)
            ?? throw new NotFoundException("InventoryItem", id);

        item.Activo = false;
        await _context.SaveChangesAsync();
        _logger.LogInformation("InventoryItem desactivado (borrado lǸgico): {Id}", id);
    }

    // ── Registrar movimiento ──────────────────────────────────────
    public async Task<InventoryMovementDto> RegistrarMovimientoAsync(
        RegisterMovementRequestDto request, Guid usuarioId)
    {
        var item = await _context.InventoryItems.FindAsync(request.InventoryItemId)
            ?? throw new NotFoundException("InventoryItem", request.InventoryItemId);

        var tipo = request.Tipo.ToUpper();
        if (tipo != "ENTRADA" && tipo != "SALIDA" && tipo != "AJUSTE")
            throw new AppException("Tipo de movimiento invǭlido. Use: ENTRADA, SALIDA o AJUSTE.");

        var stockAntes = item.StockActual;

        item.StockActual = tipo switch
        {
            "ENTRADA" => item.StockActual + request.Cantidad,
            "SALIDA"  => item.StockActual - request.Cantidad,
            "AJUSTE"  => request.Cantidad,
            _         => item.StockActual
        };

        if (item.StockActual < 0)
            throw new AppException(
                $"Stock insuficiente. Stock actual: {stockAntes}, se intenta retirar: {request.Cantidad}");

        var movimiento = new InventoryMovement
        {
            Id              = Guid.NewGuid(),
            InventoryItemId = item.Id,
            TipoMovimiento  = tipo,
            Cantidad        = request.Cantidad,
            Motivo          = request.Motivo,
            UsuarioId       = usuarioId,
            FechaHora       = DateTime.UtcNow
        };

        _context.InventoryMovements.Add(movimiento);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Movimiento {Tipo} en item {Id}: {Antes}→{Despues}",
            tipo, item.Id, stockAntes, item.StockActual);

        return new InventoryMovementDto
        {
            Id              = movimiento.Id,
            InventoryItemId = item.Id,
            NombreItem      = item.Nombre,
            Tipo            = tipo,
            Cantidad        = request.Cantidad,
            StockAntes      = stockAntes,
            StockDespues    = item.StockActual,
            Motivo          = request.Motivo,
            FechaHora       = movimiento.FechaHora
        };
    }

    // ── Listar movimientos ────────────────────────────────────────
    public async Task<PagedResultDto<InventoryMovementDto>> ListarMovimientosAsync(
        Guid? inventoryItemId, int page, int size)
    {
        if (page <= 0) page = 1;
        if (size <= 0) size = 10;

        var query = _context.InventoryMovements
            .Include(m => m.InventoryItem)
            .AsQueryable();

        if (inventoryItemId.HasValue)
            query = query.Where(m => m.InventoryItemId == inventoryItemId.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(m => m.FechaHora)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(m => new InventoryMovementDto
            {
                Id              = m.Id,
                InventoryItemId = m.InventoryItemId,
                NombreItem      = m.InventoryItem.Nombre,
                Tipo            = m.TipoMovimiento,
                Cantidad        = m.Cantidad,
                Motivo          = m.Motivo,
                FechaHora       = m.FechaHora
            })
            .ToListAsync();

        return new PagedResultDto<InventoryMovementDto>
        {
            Items        = items,
            Total        = total,
            Pagina       = page,
            TamanoPagina = size,
            TotalPaginas = (int)Math.Ceiling(total / (double)size)
        };
    }

    // ── Historial y Predicción ────────────────────────────────────
    public async Task RegistrarSnapshotDiarioAsync()
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        var existe = await _context.InventoryDailySnapshots.AnyAsync(s => s.Fecha == hoy);
        if (existe) {
            _logger.LogWarning("Ya existe un snapshot para la fecha {Fecha}. Omitiendo.", hoy);
            return;
        }

        var inicioDia = hoy.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var finDia    = hoy.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        // Un solo JOIN: obtiene stock actual + totales de movimientos del día por item
        var datos = await _context.InventoryItems
            .Where(i => i.Activo)
            .Select(i => new
            {
                i.Id,
                i.StockActual,
                CantidadVendida  = i.InventoryMovements
                    .Where(m => m.TipoMovimiento == "SALIDA" && m.FechaHora >= inicioDia && m.FechaHora <= finDia)
                    .Sum(m => (int?)m.Cantidad) ?? 0,
                CantidadRecibida = i.InventoryMovements
                    .Where(m => m.TipoMovimiento == "ENTRADA" && m.FechaHora >= inicioDia && m.FechaHora <= finDia)
                    .Sum(m => (int?)m.Cantidad) ?? 0
            })
            .ToListAsync();

        foreach (var d in datos)
        {
            _context.InventoryDailySnapshots.Add(new InventoryDailySnapshot
            {
                Id               = Guid.NewGuid(),
                InventoryItemId  = d.Id,
                Fecha            = hoy,
                StockFinal       = d.StockActual,
                CantidadVendida  = d.CantidadVendida,
                CantidadRecibida = d.CantidadRecibida
            });
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Snapshot diario completado para {Fecha} ({Count} items)", hoy, datos.Count);
    }

    public async Task<InventoryHistoryDto> ObtenerHistorialAsync(Guid inventoryItemId)
    {
        var item = await _context.InventoryItems.FindAsync(inventoryItemId)
            ?? throw new NotFoundException("InventoryItem", inventoryItemId);

        var snapshots = await _context.InventoryDailySnapshots
            .Where(s => s.InventoryItemId == inventoryItemId)
            .OrderBy(s => s.Fecha)
            .ToListAsync();

        var result = new InventoryHistoryDto();

        // 1. Diario (últimos 30 días)
        result.Diario = snapshots.TakeLast(30).Select(s => new DailyHistoryDto
        {
            Date     = s.Fecha.ToString("yyyy-MM-dd"),
            Stock    = s.StockFinal,
            Consumed = s.CantidadVendida,
            Nota     = s.CantidadRecibida > 0 ? $"Reabasto (+{s.CantidadRecibida})" : null
        }).ToList();

        // 2. Semanal (Agrupado por semana ISO)
        result.Semanal = snapshots
            .GroupBy(s => {
                var day = s.Fecha.ToDateTime(TimeOnly.MinValue);
                var week = System.Globalization.ISOWeek.GetWeekOfYear(day);
                return $"{day.Year}-W{week:D2}";
            })
            .Select(g => new WeeklyHistoryDto
            {
                Week     = g.Key,
                Label    = $"Sem {g.Key.Split("-W")[1]}",
                Consumed = g.Sum(s => s.CantidadVendida),
                Restock  = g.Sum(s => s.CantidadRecibida),
                Merma    = 0 // TODO: Implementar lógica de merma si se requiere
            })
            .ToList();

        // 3. Mensual
        result.Mensual = snapshots
            .GroupBy(s => s.Fecha.ToString("yyyy-MM"))
            .Select(g => {
                var totalConsumed = g.Sum(s => s.CantidadVendida);
                var weeksCount    = g.Select(s => System.Globalization.ISOWeek.GetWeekOfYear(s.Fecha.ToDateTime(TimeOnly.MinValue))).Distinct().Count();
                
                return new MonthlyHistoryDto
                {
                    Month         = g.Key,
                    Label         = g.First().Fecha.ToString("MMMM yyyy", new System.Globalization.CultureInfo("es-MX")),
                    TotalConsumed = totalConsumed,
                    TotalRestock  = g.Sum(s => s.CantidadRecibida),
                    AvgWeekly     = weeksCount > 0 ? (decimal)totalConsumed / weeksCount : 0
                };
            })
            .ToList();

        return result;
    }

    // ── Solución 1 (modelos predictivos): predicción de consumo semanal ───────────────────
    //
    // El backend arma las variables desde la base de datos y delega la INFERENCIA al sidecar
    // ml-service, que carga el mismo modelo_surtido.pkl que generó la libreta
    // 06_Notebooks/01_regresion_surtido.ipynb. Aquí NO se reimplementa el algoritmo: hacerlo
    // obligaría a replicar un bosque de 400 árboles en C# y produciría números distintos a
    // los de la libreta, que es justo lo que el §13 del proyecto no acepta.
    //
    // Momento de la predicción: se toma como semana `t` la última semana ISO COMPLETA y se
    // predice `t+1`. Es exactamente el contrato con el que se entrenó el modelo.
    public async Task<SupplyForecastDto> ObtenerPrediccionSurtidoAsync(Guid inventoryItemId)
    {
        var item = await _context.InventoryItems.FindAsync(inventoryItemId)
            ?? throw new NotFoundException("InventoryItem", inventoryItemId);

        var festividades = await _context.Catalogos
            .Where(c => c.Activo && c.MesDiaInicio != null && c.MesDiaFin != null)
            .Select(c => new { c.Nombre, c.MesDiaInicio, c.MesDiaFin })
            .ToListAsync();

        var movimientos = await _context.InventoryMovements
            .Where(m => m.InventoryItemId == inventoryItemId && m.TipoMovimiento == "SALIDA")
            .OrderBy(m => m.FechaHora)
            .Select(m => new { m.FechaHora, m.Cantidad })
            .ToListAsync();

        string? TemporadaDeSemana(DateOnly lunes)
        {
            for (var i = 0; i < 7; i++)
            {
                var mesDia = lunes.AddDays(i).ToString("MM-dd");
                var f = festividades.FirstOrDefault(x => EnVentanaMesDia(mesDia, x.MesDiaInicio!, x.MesDiaFin!));
                if (f != null) return f.Nombre;
            }
            return null;
        }

        static DateOnly LunesDe(DateTime fecha) => DateOnly.FromDateTime(
            System.Globalization.ISOWeek.ToDateTime(
                System.Globalization.ISOWeek.GetYear(fecha),
                System.Globalization.ISOWeek.GetWeekOfYear(fecha),
                DayOfWeek.Monday));

        static string EtiquetaSemana(DateOnly lunes)
        {
            var d = lunes.ToDateTime(TimeOnly.MinValue);
            return $"{System.Globalization.ISOWeek.GetYear(d)}-W{System.Globalization.ISOWeek.GetWeekOfYear(d):D2}";
        }

        // Semana t = última semana ISO completa. Objetivo = t+1 = la semana en curso.
        var lunesActual   = LunesDe(DateTime.UtcNow.Date);
        var lunesT        = lunesActual.AddDays(-7);
        var lunesObjetivo = lunesActual;

        // ── Rejilla semanal SIN huecos ────────────────────────────────────────────────────
        // Igual que en 04_ETL/s1_extraccion_surtido.sql: las semanas sin movimiento valen 0.
        // Sin esto los rezagos apuntarían a la última semana CON movimiento en vez de a la
        // semana anterior real, y el modelo recibiría variables distintas a las de su
        // entrenamiento.
        var consumoPorSemana = movimientos
            .GroupBy(m => LunesDe(m.FechaHora))
            .ToDictionary(g => g.Key, g => (decimal)g.Sum(x => x.Cantidad));

        var historico = new List<WeeklySupplyPointDto>();
        if (consumoPorSemana.Count > 0)
        {
            for (var l = consumoPorSemana.Keys.Min(); l <= lunesT; l = l.AddDays(7))
            {
                historico.Add(new WeeklySupplyPointDto
                {
                    Semana            = EtiquetaSemana(l),
                    InicioSemana      = l,
                    CantidadConsumida = (int)consumoPorSemana.GetValueOrDefault(l, 0m),
                    Temporada         = TemporadaDeSemana(l)
                });
            }
        }

        decimal ConsumoDe(DateOnly lunes) => consumoPorSemana.GetValueOrDefault(lunes, 0m);
        decimal Promedio(int semanas) => Math.Round(
            Enumerable.Range(0, semanas).Select(i => ConsumoDe(lunesT.AddDays(-7 * i))).Average(), 2);

        var temporadaObjetivo = TemporadaDeSemana(lunesObjetivo);
        var cantT  = ConsumoDe(lunesT);
        var cantT1 = ConsumoDe(lunesT.AddDays(-7));
        var cantT2 = ConsumoDe(lunesT.AddDays(-14));

        // Referencia interanual: la misma semana del año anterior respecto de la semana
        // OBJETIVO (52 semanas antes de t+1), no respecto de t. En el ETL esto es LAG(51).
        var cantAnioAnterior = ConsumoDe(lunesObjetivo.AddDays(-364));

        // Amplitud de demanda: pedidos DISTINTOS que consumieron el insumo durante la semana t.
        var finT = lunesT.AddDays(7);
        var numPedidos = await _context.OrderItems
            .Where(oi => oi.Order.EstadoPedido == "ENTREGADO"
                      && oi.Order.FechaCreacion >= lunesT.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
                      && oi.Order.FechaCreacion <  finT.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
            .Join(_context.ProductRecipes.Where(r => r.InventoryItemId == inventoryItemId),
                  oi => oi.ProductId, r => r.ProductId, (oi, r) => oi.OrderId)
            .Distinct()
            .CountAsync();

        var features = new SupplyFeaturesDto
        {
            MesObjetivo                 = lunesObjetivo.Month,
            EsTemporadaAlta             = temporadaObjetivo != null ? 1 : 0,
            CantSemanaActual            = cantT,
            CantSemanaAnterior          = cantT1,
            Cant2SemanasAtras           = cantT2,
            PromedioMovil4              = Promedio(4),
            PromedioMovil8              = Promedio(8),
            CantMismaSemanaAnioAnterior = cantAnioAnterior,
            NumPedidosSemanaActual      = numPedidos,
            EsFlorPrimaria              = item.EsFlorPrimaria ? 1 : 0,
            VariacionPctSemanaAnterior  = cantT1 > 0 ? Math.Round((cantT - cantT1) / cantT1, 4) : 0m,
            TemporadaObjetivo           = temporadaObjetivo ?? "SIN_TEMPORADA",
            UnidadMedida                = string.IsNullOrWhiteSpace(item.UnidadMedida) ? "TALLO" : item.UnidadMedida!
        };

        // ── Caso sin resultado posible ────────────────────────────────────────────────────
        // El §7 exige manejar "casos en los que no sea posible generar un resultado".
        if (movimientos.Count == 0)
        {
            return new SupplyForecastDto
            {
                InventoryItemId   = item.Id,
                Nombre            = item.Nombre,
                UnidadMedida      = item.UnidadMedida,
                Historico         = historico,
                SemanaObjetivo    = EtiquetaSemana(lunesObjetivo),
                TemporadaObjetivo = temporadaObjetivo,
                ConsumoPredicho   = 0,
                CantidadSugerida  = 0,
                StockActual       = item.StockActual,
                CoberturaStockPct = 100,
                MetodoCalculo     = "Este insumo no tiene movimientos de salida registrados, así que " +
                                    "no hay historial con el que estimar su consumo. No se genera predicción."
            };
        }

        var prediccion       = await _mlClient.PredecirSurtidoAsync(features);
        var consumoPredicho  = Math.Max(0, prediccion.ConsumoPredicho);
        var cantidadSugerida = (int)Math.Round(Math.Max(0, consumoPredicho - item.StockActual),
                                               MidpointRounding.AwayFromZero);

        _logger.LogInformation(
            "Predicción de surtido {Insumo} ({Semana}): consumo {Consumo}, stock {Stock}, sugerido {Sugerido}. Modelo {Algoritmo}.",
            item.Nombre, EtiquetaSemana(lunesObjetivo), consumoPredicho, item.StockActual,
            cantidadSugerida, prediccion.Algoritmo);

        var unidad = item.UnidadMedida ?? "u";
        var explicacion =
            $"Modelo {prediccion.Algoritmo} (scikit-learn {prediccion.VersionSklearn}), entrenado en la libreta " +
            $"01_regresion_surtido.ipynb. Consumo estimado para {EtiquetaSemana(lunesObjetivo)}: " +
            $"{consumoPredicho:F0} {unidad}. " +
            (temporadaObjetivo != null
                ? $"La semana cae en '{temporadaObjetivo}'; el año pasado esa misma semana se consumieron {cantAnioAnterior:F0} {unidad}. "
                : $"Basado en el consumo reciente (media de 4 semanas: {features.PromedioMovil4:F0} {unidad}). ") +
            $"Surtido sugerido = consumo estimado − stock actual ({item.StockActual}).";

        return new SupplyForecastDto
        {
            InventoryItemId   = item.Id,
            Nombre            = item.Nombre,
            UnidadMedida      = item.UnidadMedida,
            Historico         = historico,
            SemanaObjetivo    = EtiquetaSemana(lunesObjetivo),
            TemporadaObjetivo = temporadaObjetivo,
            ConsumoPredicho   = (int)Math.Round(consumoPredicho, MidpointRounding.AwayFromZero),
            CantidadSugerida  = cantidadSugerida,
            StockActual       = item.StockActual,
            CoberturaStockPct = consumoPredicho > 0
                ? Math.Round(item.StockActual / consumoPredicho * 100, 1)
                : 100,
            MetodoCalculo     = explicacion
        };
    }

    // Lista de reabastecimiento: cada insumo con historial de salidas, acompañado de la
    // predicción del modelo S1 (consumo estimado y cantidad sugerida a surtir la próxima semana).
    // Es la vista operativa de la Solución 1: qué y cuánto pedirle al proveedor.
    public async Task<List<SupplyReplenishmentItemDto>> ObtenerReabastecimientoAsync(bool refresh = false)
    {
        if (!refresh && _cache.TryGetValue(CacheReabastecimiento, out List<SupplyReplenishmentItemDto>? cacheada) && cacheada is not null)
            return cacheada;

        var lista = await CalcularReabastecimientoAsync();
        _cache.Set(CacheReabastecimiento, lista, TtlReabastecimiento);
        return lista;
    }

    private async Task<List<SupplyReplenishmentItemDto>> CalcularReabastecimientoAsync()
    {
        // El modelo solo predice insumos con historial de consumo (SALIDA).
        var idsConMovimiento = await _context.InventoryMovements
            .Where(m => m.TipoMovimiento == "SALIDA")
            .Select(m => m.InventoryItemId)
            .Distinct()
            .ToListAsync();

        var stockMin = await _context.InventoryItems
            .Where(i => i.Activo && idsConMovimiento.Contains(i.Id))
            .Select(i => new { i.Id, i.StockMinimo })
            .ToDictionaryAsync(i => i.Id, i => i.StockMinimo);

        var lista = new List<SupplyReplenishmentItemDto>();
        foreach (var id in stockMin.Keys)
        {
            try
            {
                var f = await ObtenerPrediccionSurtidoAsync(id);

                // Política de compra: surtir hasta cubrir el consumo predicho O, si el insumo está
                // por debajo del mínimo, al menos reponer hasta el stock mínimo. Se toma el mayor de
                // los dos, para que un insumo bajo mínimo con consumo bajo igual se sugiera comprar.
                var objetivo         = Math.Max(f.ConsumoPredicho, stockMin[id]);
                var cantidadSugerida = Math.Max(0, objetivo - f.StockActual);

                lista.Add(new SupplyReplenishmentItemDto
                {
                    InventoryItemId   = f.InventoryItemId,
                    Nombre            = f.Nombre,
                    UnidadMedida      = f.UnidadMedida,
                    StockActual       = f.StockActual,
                    StockMinimo       = stockMin[id],
                    ConsumoPredicho   = f.ConsumoPredicho,
                    CantidadSugerida  = cantidadSugerida,
                    SemanaObjetivo    = f.SemanaObjetivo,
                    TemporadaObjetivo = f.TemporadaObjetivo,
                    BajoMinimo        = f.StockActual <= stockMin[id],
                });
            }
            catch (Exception ex)
            {
                // Un insumo que falle (p. ej. el sidecar rechaza sus variables) no debe tumbar la lista.
                _logger.LogWarning(ex, "No se pudo predecir el surtido del insumo {Id} para reabastecimiento", id);
            }
        }

        // Más urgentes primero: mayor cantidad sugerida a comprar.
        return lista
            .OrderByDescending(x => x.CantidadSugerida)
            .ThenByDescending(x => x.ConsumoPredicho)
            .ToList();
    }

    // Compara un mes-día ("MM-dd") contra la ventana [inicio, fin] de una festividad, ambos en
    // el mismo formato sin año. Si fin < inicio (ej. Navidad: 12-15 a 01-06), la ventana cruza
    // el fin de año y se evalúa como unión de dos rangos en vez de un BETWEEN directo.
    private static bool EnVentanaMesDia(string mesDia, string inicio, string fin)
    {
        return string.Compare(inicio, fin, StringComparison.Ordinal) <= 0
            ? string.Compare(mesDia, inicio, StringComparison.Ordinal) >= 0 && string.Compare(mesDia, fin, StringComparison.Ordinal) <= 0
            : string.Compare(mesDia, inicio, StringComparison.Ordinal) >= 0 || string.Compare(mesDia, fin, StringComparison.Ordinal) <= 0;
    }

    public async Task<InventoryKpisDto> ObtenerKpisAsync()
    {
        var query = _context.InventoryItems.Where(i => i.Activo);

        var totalRegistros = await query.CountAsync();
        var bajoMinimo = await query.CountAsync(i => i.StockActual <= i.StockMinimo);
        var sumaAlCosto = await query.CountAsync(i => i.SumaAlCosto);
        var sucursales = await query
            .Where(i => !string.IsNullOrEmpty(i.Sucursal))
            .Select(i => i.Sucursal)
            .Distinct()
            .CountAsync();

        return new InventoryKpisDto
        {
            TotalRegistros = totalRegistros,
            BajoMinimo     = bajoMinimo,
            SumaAlCosto    = sumaAlCosto,
            Sucursales     = sucursales
        };
    }

    // ── Consumo reciente (derivado, no hay tabla de ventas por insumo) ─────
    // Cruza OrderItems (pedidos no cancelados, entregados en el rango) con ProductRecipe
    // para calcular cuántas unidades de cada insumo se han consumido. Los insumos que no
    // aparecen en el resultado nunca se vendieron en ese periodo (consumo implícito = 0).
    public async Task<Dictionary<Guid, int>> ObtenerConsumoRecienteAsync(int dias = 30)
    {
        var desde = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-dias);

        return await _context.OrderItems
            .Include(oi => oi.Order)
            .Where(oi => oi.Order.EstadoPedido != "CANCELADO" && oi.Order.FechaEntrega >= desde)
            .Join(_context.ProductRecipes, oi => oi.ProductId, r => r.ProductId,
                  (oi, r) => new { r.InventoryItemId, Unidades = oi.Cantidad * r.CantidadRequerida })
            .GroupBy(x => x.InventoryItemId)
            .Select(g => new { g.Key, Total = g.Sum(x => x.Unidades) })
            .ToDictionaryAsync(x => x.Key, x => x.Total);
    }

    public async Task<InventoryItemDto?> ResolverCoincidenciaInsumoAsync(string termino)
    {
        if (string.IsNullOrWhiteSpace(termino)) return null;

        var terminoNorm = NormalizarTexto(termino);
        if (string.IsNullOrEmpty(terminoNorm)) return null;

        // Pre-filtro en SQL: trae solo ítems cuyo nombre contenga alguna palabra del término.
        // Reduce drásticamente los registros que se traen a memoria antes del fuzzy matching.
        var palabrasPrincipales = terminoNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var baseQuery = _context.InventoryItems.Where(i => i.Activo);

        IQueryable<InventoryItem> candidateQuery = baseQuery;
        if (palabrasPrincipales.Length > 0)
        {
            var primera = palabrasPrincipales[0];
            candidateQuery = baseQuery.Where(i => EF.Functions.ILike(i.Nombre, $"%{primera}%"));

            // OR con el resto de palabras para ampliar la red
            foreach (var palabra in palabrasPrincipales.Skip(1))
            {
                var p = palabra;
                candidateQuery = candidateQuery.Union(baseQuery.Where(i => EF.Functions.ILike(i.Nombre, $"%{p}%")));
            }
        }

        var items = await candidateQuery.ToListAsync();

        // Si la búsqueda en SQL no trajo nada, intentamos con todos (fallback para Levenshtein)
        if (items.Count == 0)
        {
            items = await _context.InventoryItems.Where(i => i.Activo).ToListAsync();
        }

        if (items.Count == 0) return null;

        var matches = new List<(InventoryItem Item, double Score)>();

        foreach (var item in items)
        {
            var nombreNorm = NormalizarTexto(item.Nombre);
            if (string.IsNullOrEmpty(nombreNorm)) continue;

            // 1. Coincidencia exacta
            if (nombreNorm == terminoNorm)
            {
                matches.Add((item, 1.0));
                continue;
            }

            // 2. Coincidencia por subcadena mutua
            if (nombreNorm.Contains(terminoNorm) || terminoNorm.Contains(nombreNorm))
            {
                double ratio = (double)Math.Min(nombreNorm.Length, terminoNorm.Length) / Math.Max(nombreNorm.Length, terminoNorm.Length);
                matches.Add((item, 0.8 + (ratio * 0.15)));
                continue;
            }

            // 3. Coincidencia por palabras clave (tokens) y des-pluralización
            var tokensTermino = ObtenerTokensNormalizados(terminoNorm);
            var tokensNombre  = ObtenerTokensNormalizados(nombreNorm);

            if (tokensTermino.Count == 0 || tokensNombre.Count == 0) continue;

            int palabrasCoincidentes = tokensTermino.Count(t => tokensNombre.Contains(t));

            if (palabrasCoincidentes > 0)
            {
                double scorePalabras = (double)palabrasCoincidentes / Math.Max(tokensTermino.Count, tokensNombre.Count);
                matches.Add((item, scorePalabras * 0.7));
                continue;
            }

            // 4. Distancia de Levenshtein para variaciones menores
            int dist       = LevenshteinDistance(terminoNorm, nombreNorm);
            int maxLen     = Math.Max(terminoNorm.Length, nombreNorm.Length);
            double similarity = 1.0 - ((double)dist / maxLen);

            if (similarity >= 0.45)
            {
                matches.Add((item, similarity * 0.5));
            }
        }

        if (!matches.Any()) return null;

        var mejorMatch = matches
            .OrderByDescending(m => m.Score)
            .ThenBy(m => m.Item.Nombre.Length)
            .First();

        if (mejorMatch.Score < 0.35) return null;

        return MapToDto(mejorMatch.Item);
    }

    private static string NormalizarTexto(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

        var t = texto.ToLower().Trim();

        var normalizedString = t.Normalize(System.Text.NormalizationForm.FormD);
        var stringBuilder = new System.Text.StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        t = stringBuilder.ToString().Normalize(System.Text.NormalizationForm.FormC);
        t = _nonAlphaNumRegex.Replace(t, "");

        return t;
    }

    private static List<string> ObtenerTokensNormalizados(string textoNorm)
    {
        var palabras = textoNorm.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var stopWords = new HashSet<string> { "de", "del", "la", "las", "el", "los", "un", "una", "y", "con", "en", "para", "por" };
        var tokens = new List<string>();

        foreach (var p in palabras)
        {
            if (stopWords.Contains(p)) continue;

            var token = p;
            if (token.EndsWith("es") && token.Length > 4)
            {
                token = token.Substring(0, token.Length - 2);
            }
            else if (token.EndsWith("s") && token.Length > 3 && !token.EndsWith("is") && !token.EndsWith("us"))
            {
                token = token.Substring(0, token.Length - 1);
            }

            tokens.Add(token);
        }

        return tokens;
    }

    private static int LevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s)) return string.IsNullOrEmpty(t) ? 0 : t.Length;
        if (string.IsNullOrEmpty(t)) return s.Length;

        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; d[i, 0] = i++) { }
        for (int j = 0; j <= m; d[0, j] = j++) { }

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
    }

    private static InventoryItemDto MapToDto(InventoryItem i) => new()
    {
        Id           = i.Id,
        Nombre       = i.Nombre,
        StockActual  = i.StockActual,
        StockMinimo  = i.StockMinimo,
        Sucursal     = i.Sucursal,
        SumaAlCosto  = i.SumaAlCosto,
        UnidadMedida = i.UnidadMedida,
        PrecioCosto  = i.PrecioCosto,
        EsFlorPrimaria = i.EsFlorPrimaria,
        ImagenUrl    = i.ImagenUrl,
        Activo       = i.Activo
    };
}

