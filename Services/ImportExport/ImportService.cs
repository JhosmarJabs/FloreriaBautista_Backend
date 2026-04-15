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
    // CSV: nombre,descripcion,precio_base,tipo,es_personalizable,estado,visibilidad,imagen_url,categorias,colecciones
    public async Task<ImportResultDto> ImportarProductosAsync(Stream csv, string nombreArchivo)
    {
        var sw = Stopwatch.StartNew();
        var dto = new ImportResultDto { Archivo = nombreArchivo, EjecutadoEn = DateTime.UtcNow };

        using var ms = new MemoryStream();
        await csv.CopyToAsync(ms);
        var bytes = ms.ToArray();
        var encoding = DetectarEncoding(bytes);

        var texto = encoding.GetString(bytes);
        var lineas = texto
            .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        var datos = lineas.Skip(1).ToList();
        dto.TotalFilas = datos.Count;
        var separador = lineas.Count > 0 ? DetectarSeparador(lineas[0]) : ',';

        var categoriasDb = await _context.Categories.ToListAsync();
        var coleccionesDb = await _context.Collections.ToListAsync();
        var productosExist = await _context.Products
            .Include(p => p.ProductCategories)
            .Include(p => p.ProductCollections)
            .ToListAsync();

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
                var precioBase = decimal.TryParse(Col(cols, "preciobase", Col(cols, "precio_base", "0")),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var p) ? p : 0;
                var tipo = Col(cols, "tipo", "ARREGLO");
                var esPersonalizable = Col(cols, "espersonalizable", "false").ToLower() == "true";
                var estado = Col(cols, "estado", "ACTIVO").ToUpper();
                var visibilidad = Col(cols, "visibilidad", "AMBOS").ToUpper();
                
                var categoriasStr = Col(cols, "categorias");
                var coleccionesStr = Col(cols, "colecciones");
                var categoriasNom = categoriasStr.Split('|', StringSplitOptions.RemoveEmptyEntries);
                var coleccionesNom = coleccionesStr.Split('|', StringSplitOptions.RemoveEmptyEntries);

                if (estado != "ACTIVO" && estado != "INACTIVO") estado = "ACTIVO";
                if (visibilidad != "WEB" && visibilidad != "SOLO_SUCURSAL" && visibilidad != "AMBOS")
                    visibilidad = "AMBOS";

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
                    productoExistente.Descripcion = descripcion;
                    productoExistente.PrecioBase = precioBase;
                    productoExistente.Tipo = tipo;
                    productoExistente.EsPersonalizable = esPersonalizable;
                    productoExistente.Estado = estado;
                    productoExistente.Visibilidad = visibilidad;

                    _context.RemoveRange(productoExistente.ProductCategories);
                    foreach (var catNom in categoriasNom)
                    {
                        var cat = categoriasDb.FirstOrDefault(c =>
                            c.Nombre.Equals(catNom.Trim(), StringComparison.OrdinalIgnoreCase));
                        if (cat != null)
                            _context.Add(new ProductCategory { ProductId = productoExistente.Id, CategoryId = cat.Id });
                    }

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
                    var producto = new Product
                    {
                        Id = Guid.NewGuid(),
                        Nombre = nombre,
                        Descripcion = descripcion,
                        PrecioBase = precioBase,
                        Tipo = tipo,
                        EsPersonalizable = esPersonalizable,
                        Estado = estado,
                        Visibilidad = visibilidad,
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
    // CSV: nombre,stock_actual,stock_minimo,sucursal,suma_al_costo,unidad_medida
    // Upsert por nombre+sucursal
    public async Task<ImportResultDto> ImportarInventarioAsync(Stream csv, string nombreArchivo)
    {
        var sw = Stopwatch.StartNew();
        var dto = new ImportResultDto { Archivo = nombreArchivo, EjecutadoEn = DateTime.UtcNow };

        using var ms = new MemoryStream();
        await csv.CopyToAsync(ms);
        var bytes = ms.ToArray();
        var encoding = DetectarEncoding(bytes);

        var texto = encoding.GetString(bytes);
        var lineas = texto
            .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        var datos = lineas.Skip(1).ToList();
        dto.TotalFilas = datos.Count;
        var separador = lineas.Count > 0 ? DetectarSeparador(lineas[0]) : ',';

        var encabezados = ParsearCsv(lineas[0], separador)
            .Select((h, i) => (h.Trim().ToLower(), i))
            .ToDictionary(x => x.Item1, x => x.Item2);

        string Col(string[] cols, string key, string def = "") =>
            encabezados.TryGetValue(key, out var idx2) && idx2 < cols.Length
                ? cols[idx2].Trim() : def;

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

                var nombre = Col(cols, "nombre");
                if (string.IsNullOrEmpty(nombre))
                {
                    dto.Errores++;
                    dto.DetalleErrores.Add($"Fila {idx}: 'nombre' es requerido");
                    continue;
                }

                var stockActual = int.TryParse(Col(cols, "stock_actual", "0"), out var sa) ? sa : 0;
                var stockMinimo = int.TryParse(Col(cols, "stock_minimo", "0"), out var sm) ? sm : 0;
                var sucursal = Col(cols, "sucursal", "PRINCIPAL").ToUpper();
                var sumaAlCostoRaw = Col(cols, "suma_al_costo", "true").ToLower();
                var sumaAlCosto = sumaAlCostoRaw is "true" or "1" or "si" or "sí";
                
                var precioCosto = decimal.TryParse(Col(cols, "precio_costo", "0"), 
                    System.Globalization.NumberStyles.Any, 
                    System.Globalization.CultureInfo.InvariantCulture, out var pc) ? pc : 0;
                
                var esFlorPrimariaRaw = Col(cols, "es_flor_primaria", "false").ToLower();
                var esFlorPrimaria = esFlorPrimariaRaw is "true" or "1" or "si" or "sí";
                
                var unidadMedida = Col(cols, "unidad_medida");

                var itemExistente = itemsExist.FirstOrDefault(i =>
                    i.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase) &&
                    i.Sucursal.Equals(sucursal, StringComparison.OrdinalIgnoreCase));

                if (itemExistente != null)
                {
                    itemExistente.StockActual = stockActual;
                    itemExistente.StockMinimo = stockMinimo;
                    itemExistente.SumaAlCosto = sumaAlCosto;
                    itemExistente.PrecioCosto = precioCosto;
                    itemExistente.EsFlorPrimaria = esFlorPrimaria;
                    if (!string.IsNullOrWhiteSpace(unidadMedida))
                        itemExistente.UnidadMedida = unidadMedida.ToUpper();
                    
                    dto.Actualizados++;
                }
                else
                {
                    _context.InventoryItems.Add(new InventoryItem
                    {
                        Id           = Guid.NewGuid(),
                        Nombre       = nombre,
                        StockActual  = stockActual,
                        StockMinimo  = stockMinimo,
                        Sucursal     = sucursal,
                        SumaAlCosto  = sumaAlCosto,
                        PrecioCosto  = precioCosto,
                        EsFlorPrimaria = esFlorPrimaria,
                        UnidadMedida = string.IsNullOrWhiteSpace(unidadMedida) ? null : unidadMedida.ToUpper()
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

    // ── Historial de Importaciones ────────────────────────────────
    public async Task<List<ImportJob>> ObtenerHistorialAsync()
    {
        return await _context.Set<ImportJob>()
            .OrderByDescending(j => j.CreadoEn)
            .ToListAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────
    private static Encoding DetectarEncoding(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return new UTF8Encoding(true);
        try
        {
            var utf8 = new UTF8Encoding(false, throwOnInvalidBytes: true);
            utf8.GetString(bytes);
            return utf8;
        }
        catch
        {
            return Encoding.GetEncoding("ISO-8859-1");
        }
    }

    private static char DetectarSeparador(string primeraLinea)
        => primeraLinea.Contains('\t') ? '\t' : ',';

    private static string[] ParsearCsv(string linea, char separador = ',')
    {
        if (separador == '\t')
            return linea.Split('\t').Select(c => c.Trim()).ToArray();

        var resultado = new List<string>();
        var actual = new StringBuilder();
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
                else enComillas = !enComillas;
            }
            else if (c == separador && !enComillas)
            {
                resultado.Add(actual.ToString());
                actual.Clear();
            }
            else actual.Append(c);
        }
        resultado.Add(actual.ToString());
        return resultado.ToArray();
    }
}
