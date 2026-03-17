using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.ImportExport;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Services.Interfaces;

namespace FloreriaBautista.Services.ImportExport;

public class ImportService : IImportService
{
    private readonly AppDbContext           _context;
    private readonly ILogger<ImportService> _logger;

    public ImportService(AppDbContext context, ILogger<ImportService> logger)
    {
        _context = context;
        _logger  = logger;
    }

    // ── Importar Productos ────────────────────────────────────────
    // CSV esperado: nombre,descripcion,precio_base,tipo,es_personalizable,estado,imagen_url,categorias,colecciones
    // Si existe un producto con el mismo nombre → actualiza. Si no → inserta.
    public async Task<ImportResultDto> ImportarProductosAsync(Stream csv, string nombreArchivo)
    {
        var sw  = Stopwatch.StartNew();
        var dto = new ImportResultDto { Archivo = nombreArchivo, EjecutadoEn = DateTime.UtcNow };

        using var reader = new StreamReader(csv);
        var lineas = new List<string>();
        while (!reader.EndOfStream)
        {
            var linea = await reader.ReadLineAsync();
            if (!string.IsNullOrWhiteSpace(linea))
                lineas.Add(linea);
        }

        // Saltar encabezado
        var datos = lineas.Skip(1).ToList();
        dto.TotalFilas = datos.Count;

        // Cargar categorías y colecciones existentes para mapeo por nombre
        var categoriasDb    = await _context.Categories.ToListAsync();
        var coleccionesDb   = await _context.Collections.ToListAsync();
        var productosExist  = await _context.Products
            .Include(p => p.ProductCategories)
            .Include(p => p.ProductCollections)
            .ToListAsync();

        foreach (var (linea, idx) in datos.Select((l, i) => (l, i + 2)))
        {
            try
            {
                var cols = ParsearCsv(linea);
                if (cols.Length < 7)
                {
                    dto.Errores++;
                    dto.DetalleErrores.Add($"Fila {idx}: columnas insuficientes ({cols.Length})");
                    continue;
                }

                var nombre          = cols[0].Trim();
                var descripcion     = cols.Length > 1 ? cols[1].Trim() : "";
                var precioBase      = decimal.TryParse(cols[2].Trim(), out var p) ? p : 0;
                var tipo            = cols.Length > 3 ? cols[3].Trim() : "ARREGLO";
                var esPersonalizable = cols.Length > 4 && cols[4].Trim().ToLower() == "true";
                var estado          = cols.Length > 5 ? cols[5].Trim() : "ACTIVO";
                var imagenUrl       = cols.Length > 6 ? cols[6].Trim() : null;
                var categoriasNom   = cols.Length > 7 ? cols[7].Split('|', StringSplitOptions.RemoveEmptyEntries) : [];
                var coleccionesNom  = cols.Length > 8 ? cols[8].Split('|', StringSplitOptions.RemoveEmptyEntries) : [];

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
                    productoExistente.Descripcion      = descripcion;
                    productoExistente.PrecioBase       = precioBase;
                    productoExistente.Tipo             = tipo;
                    productoExistente.EsPersonalizable = esPersonalizable;
                    productoExistente.Estado           = estado;
                    productoExistente.ImagenUrl        = string.IsNullOrEmpty(imagenUrl) ? null : imagenUrl;

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
                        Id              = Guid.NewGuid(),
                        Nombre          = nombre,
                        Descripcion     = descripcion,
                        PrecioBase      = precioBase,
                        Tipo            = tipo,
                        EsPersonalizable = esPersonalizable,
                        Estado          = estado,
                        ImagenUrl       = string.IsNullOrEmpty(imagenUrl) ? null : imagenUrl,
                        CreadoEn        = DateTime.UtcNow
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
    // CSV esperado: product_id,stock_actual,stock_minimo,sucursal
    // Si existe el item para ese product_id → actualiza stock. Si no → inserta.
    public async Task<ImportResultDto> ImportarInventarioAsync(Stream csv, string nombreArchivo)
    {
        var sw  = Stopwatch.StartNew();
        var dto = new ImportResultDto { Archivo = nombreArchivo, EjecutadoEn = DateTime.UtcNow };

        using var reader = new StreamReader(csv);
        var lineas = new List<string>();
        while (!reader.EndOfStream)
        {
            var linea = await reader.ReadLineAsync();
            if (!string.IsNullOrWhiteSpace(linea))
                lineas.Add(linea);
        }

        var datos = lineas.Skip(1).ToList();
        dto.TotalFilas = datos.Count;

        var productosIds = await _context.Products.Select(p => p.Id).ToListAsync();
        var itemsExist   = await _context.InventoryItems.ToListAsync();

        foreach (var (linea, idx) in datos.Select((l, i) => (l, i + 2)))
        {
            try
            {
                var cols = ParsearCsv(linea);
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
                var sucursal    = cols.Length > 3 ? cols[3].Trim() : "PRINCIPAL";

                var itemExistente = itemsExist.FirstOrDefault(i => i.ProductId == productId);

                if (itemExistente != null)
                {
                    itemExistente.StockActual = stockActual;
                    itemExistente.StockMinimo = stockMinimo;
                    itemExistente.Sucursal    = sucursal;
                    dto.Actualizados++;
                }
                else
                {
                    _context.InventoryItems.Add(new InventoryItem
                    {
                        Id          = Guid.NewGuid(),
                        ProductId   = productId,
                        StockActual = stockActual,
                        StockMinimo = stockMinimo,
                        Sucursal    = sucursal
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

    // ── Parser CSV simple ─────────────────────────────────────────
    private static string[] ParsearCsv(string linea)
    {
        var resultado = new List<string>();
        var actual    = new System.Text.StringBuilder();
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
            else if (c == ',' && !enComillas)
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
