using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using FloreriaBautista.Data;
using FloreriaBautista.Services.Interfaces;

namespace FloreriaBautista.Services.ImportExport;

public class ExportService : IExportService
{
    private readonly AppDbContext          _context;
    private readonly ILogger<ExportService> _logger;

    public ExportService(AppDbContext context, ILogger<ExportService> logger)
    {
        _context = context;
        _logger  = logger;
    }

    // ── Exportar Productos ────────────────────────────────────────
    // Columnas: id, nombre, descripcion, precio_base, tipo, es_personalizable,
    //           estado, imagen_url, categorias, colecciones, creado_en
    public async Task<(byte[] Contenido, string NombreArchivo)> ExportarProductosAsync()
    {
        var sw = Stopwatch.StartNew();

        var productos = await _context.Products
            .Include(p => p.ProductCategories).ThenInclude(pc => pc.Category)
            .Include(p => p.ProductCollections).ThenInclude(pc => pc.Collection)
            .OrderBy(p => p.Nombre)
            .ToListAsync();

        var sb = new StringBuilder();

        // Encabezado
        sb.AppendLine("id,nombre,descripcion,precio_base,tipo,es_personalizable,estado,imagen_url,categorias,colecciones,creado_en");

        foreach (var p in productos)
        {
            var categorias  = string.Join("|", p.ProductCategories.Select(pc => pc.Category.Nombre));
            var colecciones = string.Join("|", p.ProductCollections.Select(pc => pc.Collection.Nombre));

            sb.AppendLine(string.Join(",",
                p.Id,
                Escapar(p.Nombre),
                Escapar(p.Descripcion),
                p.PrecioBase.ToString("F2"),
                Escapar(p.Tipo),
                p.EsPersonalizable ? "true" : "false",
                Escapar(p.Estado),
                Escapar(p.ImagenUrl ?? ""),
                Escapar(categorias),
                Escapar(colecciones),
                p.CreadoEn.ToString("yyyy-MM-dd HH:mm:ss")
            ));
        }

        sw.Stop();
        _logger.LogInformation("Exportados {Count} productos en {Ms} ms", productos.Count, sw.ElapsedMilliseconds);

        var nombre = $"productos_{DateTime.Now:yyyyMMdd_HHmm}.csv";
        return (Encoding.UTF8.GetBytes(sb.ToString()), nombre);
    }

    // ── Exportar Inventario ───────────────────────────────────────
    // Columnas: id, product_id, nombre_producto, stock_actual, stock_minimo, sucursal
    public async Task<(byte[] Contenido, string NombreArchivo)> ExportarInventarioAsync()
    {
        var sw = Stopwatch.StartNew();

        var items = await _context.InventoryItems
            .Include(i => i.Product)
            .OrderBy(i => i.Product.Nombre)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("id,product_id,nombre_producto,stock_actual,stock_minimo,sucursal");

        foreach (var i in items)
        {
            sb.AppendLine(string.Join(",",
                i.Id,
                i.ProductId,
                Escapar(i.Product.Nombre),
                i.StockActual,
                i.StockMinimo,
                Escapar(i.Sucursal)
            ));
        }

        sw.Stop();
        _logger.LogInformation("Exportados {Count} items de inventario en {Ms} ms", items.Count, sw.ElapsedMilliseconds);

        var nombre = $"inventario_{DateTime.Now:yyyyMMdd_HHmm}.csv";
        return (Encoding.UTF8.GetBytes(sb.ToString()), nombre);
    }

    // ── Exportar Flores ───────────────────────────────────────────
    // Columnas: nombre,color,precio_costo,unidad_medida,es_flor_primaria,stock_minimo,stock_actual,estado,creado_en
    public async Task<(byte[] Contenido, string NombreArchivo)> ExportarFloresAsync()
    {
        var sw = Stopwatch.StartNew();

        var flores = await _context.Flowers
            .OrderBy(f => f.Nombre)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("nombre,color,precio_costo,unidad_medida,es_flor_primaria,stock_minimo,stock_actual,estado,creado_en");

        foreach (var f in flores)
        {
            sb.AppendLine(string.Join(",",
                Escapar(f.Nombre),
                Escapar(f.Color),
                f.PrecioCosto.ToString("F2"),
                f.UnidadMedida,
                f.EsFlorPrimaria ? "true" : "false",
                f.StockMinimo,
                f.StockActual,
                f.Estado,
                f.CreadoEn.ToString("yyyy-MM-dd HH:mm:ss")
            ));
        }

        sw.Stop();
        _logger.LogInformation("Exportadas {Count} flores en {Ms} ms", flores.Count, sw.ElapsedMilliseconds);

        var nombre = $"flores_{DateTime.Now:yyyyMMdd_HHmm}.csv";
        return (Encoding.UTF8.GetBytes(sb.ToString()), nombre);
    }

    // Escapa un valor para CSV: encierra en comillas si contiene coma, comilla o salto
    private static string Escapar(string valor)
    {
        if (valor.Contains(',') || valor.Contains('"') || valor.Contains('\n'))
            return $"\"{valor.Replace("\"", "\"\"")}\"";
        return valor;
    }
}
