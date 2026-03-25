using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.ImportExport;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Services.Interfaces;

namespace FloreriaBautista.Services.ImportExport;

public class ImportService : IImportService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ImportService> _logger;

    public ImportService(AppDbContext context, ILogger<ImportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ── Importar Productos ────────────────────────────────────────
    // CSV esperado: nombre,descripcion,precio_base,tipo,es_personalizable,estado,imagen_url,categorias,colecciones
    // Si existe un producto con el mismo nombre → actualiza. Si no → inserta.
    public async Task<ImportResultDto> ImportarProductosAsync(Stream csv, string nombreArchivo)
    {
        var sw = Stopwatch.StartNew();
        var dto = new ImportResultDto { Archivo = nombreArchivo, EjecutadoEn = DateTime.UtcNow };

        // Leer bytes crudos para detectar encoding
        using var ms = new MemoryStream();
        await csv.CopyToAsync(ms);
        var bytes = ms.ToArray();

        // Detectar BOM UTF-8, si no intentar Latin-1 (ISO-8859-1)
        Encoding encoding;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            encoding = new UTF8Encoding(true); // UTF-8 con BOM
        else
        {
            // Intentar decodificar como UTF-8; si falla usar Latin-1
            try
            {
                var utf8 = new UTF8Encoding(false, throwOnInvalidBytes: true);
                utf8.GetString(bytes); // lanza si hay bytes inválidos
                encoding = utf8;
            }
            catch
            {
                encoding = Encoding.GetEncoding("ISO-8859-1");
            }
        }

        var texto = encoding.GetString(bytes);
        var lineas = texto
            .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        // Saltar encabezado
        var datos = lineas.Skip(1).ToList();
        dto.TotalFilas = datos.Count;

        // Detectar separador automáticamente (coma o tabulador)
        var separador = lineas.Count > 0 ? DetectarSeparador(lineas[0]) : ',';

        // Cargar categorías y colecciones existentes para mapeo por nombre
        var categoriasDb = await _context.Categories.ToListAsync();
        var coleccionesDb = await _context.Collections.ToListAsync();
        var productosExist = await _context.Products
            .Include(p => p.ProductCategories)
            .Include(p => p.ProductCollections)
            .ToListAsync();

        // Mapeo por nombre de columna desde el encabezado
        var encabezados = ParsearCsv(lineas[0], separador)
            .Select((h, i) => (h.Trim().ToLower(), i))
            .ToDictionary(x => x.Item1, x => x.Item2);

        string Col(string[] cols, string key, string def = "") =>
            encabezados.TryGetValue(key, out var idx2) && idx2 < cols.Length
                ? cols[idx2].Trim() : def;

        foreach (var (linea, idx) in datos.Select((l, i) => (l, i + 2)))
        {
            try
            {
                var cols = ParsearCsv(linea, separador);
                if (cols.Length < 2)
                {
                    dto.Errores++;
                    dto.DetalleErrores.Add($"Fila {idx}: columnas insuficientes ({cols.Length})");
                    continue;
                }

                var nombre = Col(cols, "nombre");
                var descripcion = Col(cols, "descripcion");
                var precioBase = decimal.TryParse(Col(cols, "preciobase", Col(cols, "precio_base", "0")), out var p) ? p : 0;
                var tipo = Col(cols, "tipo", "ARREGLO");
                var esPersonalizable = Col(cols, "espersonalizable", "false").ToLower() == "true";
                var estado = Col(cols, "estado", "ACTIVO").ToUpper();
                var imagenUrl = Col(cols, "imagenurl", Col(cols, "imagen_url"));
                var categoriasStr = Col(cols, "categorias");
                var coleccionesStr = Col(cols, "colecciones");
                var categoriasNom = categoriasStr.Split('|', StringSplitOptions.RemoveEmptyEntries);
                var coleccionesNom = coleccionesStr.Split('|', StringSplitOptions.RemoveEmptyEntries);

                // Validar estado
                if (estado != "ACTIVO" && estado != "INACTIVO")
                    estado = "ACTIVO";

                if (string.IsNullOrEmpty(nombre))
                {
                    dto.Errores++;
                    dto.DetalleErrores.Add($"Fila {idx}: 'nombre' es requerido");
                    continue;
                }

                var productoExistente = productosExist.FirstOrDefault(pr =>
                    pr.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase));

                if (productoExistente != null)
                {
                    // Actualizar
                    productoExistente.Descripcion = descripcion;
                    productoExistente.PrecioBase = precioBase;
                    productoExistente.Tipo = tipo;
                    productoExistente.EsPersonalizable = esPersonalizable;
                    productoExistente.Estado = estado;
                    productoExistente.ImagenUrl = string.IsNullOrEmpty(imagenUrl) ? null : imagenUrl;

                    // Actualizar categorías
                    _context.RemoveRange(productoExistente.ProductCategories);
                    foreach (var catNom in categoriasNom)
                    {
                        var cat = categoriasDb.FirstOrDefault(c =>
                            c.Nombre.Equals(catNom.Trim(), StringComparison.OrdinalIgnoreCase));
                        if (cat != null)
                            _context.Add(new ProductCategory { ProductId = productoExistente.Id, CategoryId = cat.Id });
                    }

                    // Actualizar colecciones
                    _context.RemoveRange(productoExistente.ProductCollections);
                    foreach (var colNom in coleccionesNom)
                    {
                        var col = coleccionesDb.FirstOrDefault(c =>
                            c.Nombre.Equals(colNom.Trim(), StringComparison.OrdinalIgnoreCase));
                        if (col != null)
                            _context.Add(new ProductCollection { ProductId = productoExistente.Id, CollectionId = col.Id });
                    }

                    dto.Actualizados++;
                }
                else
                {
                    // Insertar nuevo
                    var producto = new Product
                    {
                        Id = Guid.NewGuid(),
                        Nombre = nombre,
                        Descripcion = descripcion,
                        PrecioBase = precioBase,
                        Tipo = tipo,
                        EsPersonalizable = esPersonalizable,
                        Estado = estado,
                        ImagenUrl = string.IsNullOrEmpty(imagenUrl) ? null : imagenUrl,
                        CreadoEn = DateTime.UtcNow
                    };
                    _context.Products.Add(producto);

                    foreach (var catNom in categoriasNom)
                    {
                        var cat = categoriasDb.FirstOrDefault(c =>
                            c.Nombre.Equals(catNom.Trim(), StringComparison.OrdinalIgnoreCase));
                        if (cat != null)
                            _context.Add(new ProductCategory { ProductId = producto.Id, CategoryId = cat.Id });
                    }

                    foreach (var colNom in coleccionesNom)
                    {
                        var col = coleccionesDb.FirstOrDefault(c =>
                            c.Nombre.Equals(colNom.Trim(), StringComparison.OrdinalIgnoreCase));
                        if (col != null)
                            _context.Add(new ProductCollection { ProductId = producto.Id, CollectionId = col.Id });
                    }

                    dto.Insertados++;
                }

                // ── Procesar stock si viene en el CSV ─────────────
                var stockStr = Col(cols, "stock", Col(cols, "stock_actual", ""));
                if (!string.IsNullOrWhiteSpace(stockStr) && int.TryParse(stockStr, out var stockVal))
                {
                    // Buscar producto por nombre para obtener su ID real
                    var prodId = productoExistente?.Id;
                    if (prodId == null)
                    {
                        // Si fue insertado, buscar por nombre
                        var prod = await _context.Products
                            .FirstOrDefaultAsync(p => p.Nombre == nombre);
                        prodId = prod?.Id;
                    }

                    if (prodId.HasValue)
                    {
                        var invItem = await _context.InventoryItems
                            .FirstOrDefaultAsync(i => i.ProductId == prodId.Value);

                        if (invItem != null)
                        {
                            invItem.StockActual = stockVal;
                        }
                        else
                        {
                            _context.InventoryItems.Add(new InventoryItem
                            {
                                Id = Guid.NewGuid(),
                                ProductId = prodId.Value,
                                StockActual = stockVal,
                                StockMinimo = 0,
                                Sucursal = "PRINCIPAL"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                dto.Errores++;
                dto.DetalleErrores.Add($"Fila {idx}: {ex.Message}");
            }
        }

        await _context.SaveChangesAsync();
        sw.Stop();
        dto.DuracionMs = sw.Elapsed.TotalMilliseconds;

        _logger.LogInformation("Importación productos: {I} insertados, {A} actualizados, {E} errores en {Ms} ms",
            dto.Insertados, dto.Actualizados, dto.Errores, dto.DuracionMs);

        return dto;
    }

    // ── Importar Inventario ───────────────────────────────────────
    // CSV esperado: product_id,stock_actual,stock_minimo,sucursal
    // Si existe el item para ese product_id → actualiza stock. Si no → inserta.
    public async Task<ImportResultDto> ImportarInventarioAsync(Stream csv, string nombreArchivo)
    {
        var sw = Stopwatch.StartNew();
        var dto = new ImportResultDto { Archivo = nombreArchivo, EjecutadoEn = DateTime.UtcNow };

        // Leer bytes crudos para detectar encoding
        using var ms = new MemoryStream();
        await csv.CopyToAsync(ms);
        var bytes = ms.ToArray();

        // Detectar BOM UTF-8, si no intentar Latin-1 (ISO-8859-1)
        Encoding encoding;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            encoding = new UTF8Encoding(true); // UTF-8 con BOM
        else
        {
            // Intentar decodificar como UTF-8; si falla usar Latin-1
            try
            {
                var utf8 = new UTF8Encoding(false, throwOnInvalidBytes: true);
                utf8.GetString(bytes); // lanza si hay bytes inválidos
                encoding = utf8;
            }
            catch
            {
                encoding = Encoding.GetEncoding("ISO-8859-1");
            }
        }

        var texto = encoding.GetString(bytes);
        var lineas = texto
            .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        var datos = lineas.Skip(1).ToList();
        dto.TotalFilas = datos.Count;

        var separador = lineas.Count > 0 ? DetectarSeparador(lineas[0]) : ',';

        var productosIds = await _context.Products.Select(p => p.Id).ToListAsync();
        var itemsExist = await _context.InventoryItems.ToListAsync();

        foreach (var (linea, idx) in datos.Select((l, i) => (l, i + 2)))
        {
            try
            {
                var cols = ParsearCsv(linea, separador);
                if (cols.Length < 2)
                {
                    dto.Errores++;
                    dto.DetalleErrores.Add($"Fila {idx}: columnas insuficientes");
                    continue;
                }

                if (!Guid.TryParse(cols[0].Trim(), out var productId))
                {
                    dto.Errores++;
                    dto.DetalleErrores.Add($"Fila {idx}: product_id inválido '{cols[0]}'");
                    continue;
                }

                if (!productosIds.Contains(productId))
                {
                    dto.Errores++;
                    dto.DetalleErrores.Add($"Fila {idx}: producto '{productId}' no existe");
                    continue;
                }

                var stockActual = int.TryParse(cols[1].Trim(), out var sa) ? sa : 0;
                var stockMinimo = cols.Length > 2 && int.TryParse(cols[2].Trim(), out var sm) ? sm : 0;
                var sucursal = cols.Length > 3 ? cols[3].Trim() : "PRINCIPAL";

                var itemExistente = itemsExist.FirstOrDefault(i => i.ProductId == productId);

                if (itemExistente != null)
                {
                    itemExistente.StockActual = stockActual;
                    itemExistente.StockMinimo = stockMinimo;
                    itemExistente.Sucursal = sucursal;
                    dto.Actualizados++;
                }
                else
                {
                    _context.InventoryItems.Add(new InventoryItem
                    {
                        Id = Guid.NewGuid(),
                        ProductId = productId,
                        StockActual = stockActual,
                        StockMinimo = stockMinimo,
                        Sucursal = sucursal
                    });
                    dto.Insertados++;
                }
            }
            catch (Exception ex)
            {
                dto.Errores++;
                dto.DetalleErrores.Add($"Fila {idx}: {ex.Message}");
            }
        }

        await _context.SaveChangesAsync();
        sw.Stop();
        dto.DuracionMs = sw.Elapsed.TotalMilliseconds;

        _logger.LogInformation("Importación inventario: {I} insertados, {A} actualizados, {E} errores en {Ms} ms",
            dto.Insertados, dto.Actualizados, dto.Errores, dto.DuracionMs);

        return dto;
    }

    // ── Importar Flores ───────────────────────────────────────────
    // CSV esperado: nombre,color,precio_costo,unidad_medida,es_flor_primaria,stock_minimo,stock_inicial
    // Upsert por nombre + color
    public async Task<ImportResultDto> ImportarFloresAsync(Stream csv, string nombreArchivo)
    {
        var sw  = Stopwatch.StartNew();
        var dto = new ImportResultDto { Archivo = nombreArchivo, EjecutadoEn = DateTime.UtcNow };

        using var ms = new MemoryStream();
        await csv.CopyToAsync(ms);
        var bytes = ms.ToArray();

        Encoding encoding;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            encoding = new UTF8Encoding(true);
        else
        {
            try
            {
                var utf8 = new UTF8Encoding(false, throwOnInvalidBytes: true);
                utf8.GetString(bytes);
                encoding = utf8;
            }
            catch { encoding = Encoding.GetEncoding("ISO-8859-1"); }
        }

        var texto  = encoding.GetString(bytes);
        var lineas = texto
            .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        var datos     = lineas.Skip(1).ToList();
        dto.TotalFilas = datos.Count;
        var separador  = lineas.Count > 0 ? DetectarSeparador(lineas[0]) : ',';

        var encabezados = ParsearCsv(lineas[0], separador)
            .Select((h, i) => (h.Trim().ToLower(), i))
            .ToDictionary(x => x.Item1, x => x.Item2);

        string Col(string[] cols, string key, string def = "") =>
            encabezados.TryGetValue(key, out var idx2) && idx2 < cols.Length
                ? cols[idx2].Trim() : def;

        var floresExist = await _context.Flowers.ToListAsync();

        foreach (var (linea, idx) in datos.Select((l, i) => (l, i + 2)))
        {
            try
            {
                var cols = ParsearCsv(linea, separador);
                if (cols.Length < 2)
                {
                    dto.Errores++;
                    dto.DetalleErrores.Add($"Fila {idx}: columnas insuficientes ({cols.Length})");
                    continue;
                }

                var nombre     = Col(cols, "nombre");
                var color      = Col(cols, "color");
                var precioCostoRaw = Col(cols, "precio_costo", Col(cols, "preciocosto", "0"));
                var unidad     = Col(cols, "unidad_medida", Col(cols, "unidadmedida", "TALLO"));
                var esPrimariaRaw = Col(cols, "es_flor_primaria", Col(cols, "esflorprimaria", "false")).ToLower();
                var stockMinimoRaw  = Col(cols, "stock_minimo",   Col(cols, "stockminimo", "0"));
                // acepta stock_actual (del export) o stock_inicial (para carga nueva)
                var stockInicialRaw = Col(cols, "stock_actual",   Col(cols, "stock_inicial",
                                      Col(cols, "stockactual",    Col(cols, "stockinicial", "0"))));

                if (string.IsNullOrEmpty(nombre) || string.IsNullOrEmpty(color))
                {
                    dto.Errores++;
                    dto.DetalleErrores.Add($"Fila {idx}: 'nombre' y 'color' son obligatorios");
                    continue;
                }

                if (!decimal.TryParse(precioCostoRaw,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var precioCosto))
                {
                    dto.Errores++;
                    dto.DetalleErrores.Add($"Fila {idx}: precio_costo inválido '{precioCostoRaw}'");
                    continue;
                }

                if (!int.TryParse(stockMinimoRaw, out var stockMinimo))  stockMinimo  = 0;
                if (!int.TryParse(stockInicialRaw, out var stockInicial)) stockInicial = 0;
                var esPrimaria = esPrimariaRaw is "true" or "1" or "si" or "sí";
                var unidadNorm = string.IsNullOrWhiteSpace(unidad) ? "TALLO" : unidad.ToUpper();

                var existente = floresExist.FirstOrDefault(f =>
                    f.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase) &&
                    f.Color.Equals(color,  StringComparison.OrdinalIgnoreCase));

                if (existente != null)
                {
                    existente.PrecioCosto    = precioCosto;
                    existente.UnidadMedida   = unidadNorm;
                    existente.EsFlorPrimaria = esPrimaria;
                    existente.StockMinimo    = stockMinimo;
                    existente.StockActual    = stockInicial;
                    existente.ActualizadoEn  = DateTime.UtcNow;
                    dto.Actualizados++;
                }
                else
                {
                    _context.Flowers.Add(new Flower
                    {
                        Id             = Guid.NewGuid(),
                        Nombre         = nombre,
                        Color          = color,
                        PrecioCosto    = precioCosto,
                        UnidadMedida   = unidadNorm,
                        EsFlorPrimaria = esPrimaria,
                        StockMinimo    = stockMinimo,
                        StockActual    = stockInicial,
                        Estado         = "ACTIVA",
                        CreadoEn       = DateTime.UtcNow,
                        ActualizadoEn  = DateTime.UtcNow
                    });
                    dto.Insertados++;
                }
            }
            catch (Exception ex)
            {
                dto.Errores++;
                dto.DetalleErrores.Add($"Fila {idx}: {ex.Message}");
            }
        }

        await _context.SaveChangesAsync();
        sw.Stop();
        dto.DuracionMs = sw.Elapsed.TotalMilliseconds;

        _logger.LogInformation("Importación flores: {I} insertadas, {A} actualizadas, {E} errores en {Ms} ms",
            dto.Insertados, dto.Actualizados, dto.Errores, dto.DuracionMs);

        return dto;
    }

    // ── Parser CSV simple ─────────────────────────────────────────
    // Detecta automáticamente si el separador es coma o tabulador
    private static char DetectarSeparador(string primeraLinea)
        => primeraLinea.Contains('\t') ? '\t' : ',';

    private static string[] ParsearCsv(string linea, char separador = ',')
    {
        // Si es tabulador, split directo sin lógica de comillas
        if (separador == '\t')
            return linea.Split('\t').Select(c => c.Trim()).ToArray();

        var resultado = new List<string>();
        var actual = new System.Text.StringBuilder();
        var enComillas = false;

        for (int i = 0; i < linea.Length; i++)
        {
            var c = linea[i];
            if (c == '"')
            {
                if (enComillas && i + 1 < linea.Length && linea[i + 1] == '"')
                {
                    actual.Append('"');
                    i++;
                }
                else
                {
                    enComillas = !enComillas;
                }
            }
            else if (c == separador && !enComillas)
            {
                resultado.Add(actual.ToString());
                actual.Clear();
            }
            else
            {
                actual.Append(c);
            }
        }
        resultado.Add(actual.ToString());
        return resultado.ToArray();
    }
}