using Microsoft.EntityFrameworkCore;
using FloreriaBautista.Data;
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

    public InventoryService(AppDbContext context, ILogger<InventoryService> logger)
    {
        _context = context;
        _logger  = logger;
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
        
        // Evitar duplicados del mismo día
        var existe = await _context.InventoryDailySnapshots.AnyAsync(s => s.Fecha == hoy);
        if (existe) {
            _logger.LogWarning("Ya existe un snapshot para la fecha {Fecha}. Omitiendo.", hoy);
            return;
        }

        var items = await _context.InventoryItems.Where(i => i.Activo).ToListAsync();
        var inicioDia = hoy.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var finDia    = hoy.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        foreach (var item in items)
        {
            var movimientosHoy = await _context.InventoryMovements
                .Where(m => m.InventoryItemId == item.Id && m.FechaHora >= inicioDia && m.FechaHora <= finDia)
                .ToListAsync();

            var snapshot = new InventoryDailySnapshot
            {
                Id               = Guid.NewGuid(),
                InventoryItemId  = item.Id,
                Fecha            = hoy,
                StockFinal       = item.StockActual,
                CantidadVendida  = movimientosHoy.Where(m => m.TipoMovimiento == "SALIDA").Sum(m => m.Cantidad),
                CantidadRecibida = movimientosHoy.Where(m => m.TipoMovimiento == "ENTRADA").Sum(m => m.Cantidad)
            };

            _context.InventoryDailySnapshots.Add(snapshot);
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Snapshot diario completado para {Fecha} ({Count} items)", hoy, items.Count);
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

    public async Task<InventoryItemDto?> ResolverCoincidenciaInsumoAsync(string termino)
    {
        if (string.IsNullOrWhiteSpace(termino)) return null;

        var items = await _context.InventoryItems
            .Where(i => i.Activo)
            .ToListAsync();

        if (!items.Any()) return null;

        var terminoNorm = NormalizarTexto(termino);
        if (string.IsNullOrEmpty(terminoNorm)) return null;

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
            var tokensNombre = ObtenerTokensNormalizados(nombreNorm);

            if (tokensTermino.Count == 0 || tokensNombre.Count == 0) continue;

            int palabrasCoincidentes = 0;
            foreach (var tokenT in tokensTermino)
            {
                if (tokensNombre.Contains(tokenT))
                {
                    palabrasCoincidentes++;
                }
            }

            if (palabrasCoincidentes > 0)
            {
                double scorePalabras = (double)palabrasCoincidentes / Math.Max(tokensTermino.Count, tokensNombre.Count);
                matches.Add((item, scorePalabras * 0.7));
                continue;
            }

            // 4. Distancia de Levenshtein para variaciones menores
            int dist = LevenshteinDistance(terminoNorm, nombreNorm);
            int maxLen = Math.Max(terminoNorm.Length, nombreNorm.Length);
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

        if (mejorMatch.Score < 0.35)
        {
            return null;
        }

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
        t = System.Text.RegularExpressions.Regex.Replace(t, @"[^a-z0-9\s]", "");

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

