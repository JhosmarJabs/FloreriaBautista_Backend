using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using FloreriaBautista.Data;
using FloreriaBautista.Services.Interfaces;

namespace FloreriaBautista.Services.ImportExport;

public class ExportService : IExportService
{
    private readonly AppDbContext           _context;
    private readonly ILogger<ExportService> _logger;

    public ExportService(AppDbContext context, ILogger<ExportService> logger)
    {
        _context = context;
        _logger  = logger;
    }

    // ── Exportar Productos ────────────────────────────────────────
    // Columnas: id,nombre,descripcion,precio_base,tipo,es_personalizable,estado,visibilidad,imagen_url,categorias,colecciones,creado_en
    public async Task<(byte[] Contenido, string NombreArchivo)> ExportarProductosAsync()
    {
        var sw = Stopwatch.StartNew();

        var productos = await _context.Products.Include(p => p.ProductCategories).ThenInclude(pc => pc.Category)
            .Include(p => p.ProductCollections).ThenInclude(pc => pc.Collection)
            .OrderBy(p => p.Nombre)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine(
            "nombre,descripcion,precio_base,tipo,es_personalizable,estado,visibilidad,categorias,colecciones,creado_en"
        );

        foreach (var p in productos)
        {
            var categorias  = string.Join("|", p.ProductCategories.Select(pc => pc.Category.Nombre));
            var colecciones = string.Join("|", p.ProductCollections.Select(pc => pc.Collection.Nombre));

            sb.AppendLine(string.Join(",",
                Escapar(p.Nombre),
                Escapar(p.Descripcion),
                p.PrecioBase.ToString("F2"),
                Escapar(p.Tipo),
                p.EsPersonalizable ? "true" : "false",
                Escapar(p.Estado),
                Escapar(p.Visibilidad),
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
    // Columnas: id,nombre,stock_actual,stock_minimo,sucursal,suma_al_costo,unidad_medida
    public async Task<(byte[] Contenido, string NombreArchivo)> ExportarInventarioAsync()
    {
        var sw = Stopwatch.StartNew();

        var items = await _context.InventoryItems
            .OrderBy(i => i.Nombre)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine(
            "nombre,stock_actual,stock_minimo,sucursal,suma_al_costo,precio_costo,es_flor_primaria,unidad_medida"
        );

        foreach (var i in items)
        {
            sb.AppendLine(string.Join(",",
                Escapar(i.Nombre),
                i.StockActual,
                i.StockMinimo,
                Escapar(i.Sucursal),
                i.SumaAlCosto ? "true" : "false",
                i.PrecioCosto.ToString("F2"),
                i.EsFlorPrimaria ? "true" : "false",
                Escapar(i.UnidadMedida ?? "")
            ));
        }

        sw.Stop();
        _logger.LogInformation("Exportados {Count} items de inventario en {Ms} ms", items.Count, sw.ElapsedMilliseconds);

        var nombre = $"inventario_{DateTime.Now:yyyyMMdd_HHmm}.csv";
        return (Encoding.UTF8.GetBytes(sb.ToString()), nombre);
    }

    private static string Escapar(string valor)
    {
        if (valor.Contains(',') || valor.Contains('"') || valor.Contains('\n'))
            return $"\"{valor.Replace("\"", "\"\"")}\"";
        return valor;
    }

    // ── Exportar Pedidos ──────────────────────────────────────────
    public async Task<(byte[] Contenido, string NombreArchivo)> ExportarPedidosAsync()
    {
        var sw = Stopwatch.StartNew();
        var pedidos = await _context.Orders
            .Include(o => o.Customer)
            .OrderByDescending(o => o.FechaCreacion)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("id_local_offline,cliente,tipo_pedido,canal,estado,fecha_creacion,fecha_entrega,hora_entrega,total,saldo_pendiente,notas,sincronizado_en");

        foreach (var p in pedidos)
        {
            var cliente = $"{p.Customer?.Nombre} {p.Customer?.Apellido}".Trim();
            sb.AppendLine(string.Join(",",
                Escapar(p.IdLocalOffline?.ToString() ?? ""),
                Escapar(cliente),
                Escapar(p.TipoPedido),
                Escapar(p.Canal),
                Escapar(p.EstadoPedido),
                p.FechaCreacion.ToString("yyyy-MM-dd HH:mm:ss"),
                p.FechaEntrega.ToString("yyyy-MM-dd"),
                p.HoraEntrega?.ToString() ?? "",
                p.Total.ToString("F2"),
                p.SaldoPendiente.ToString("F2"),
                Escapar(p.Notas ?? ""),
                p.SincronizadoEn?.ToString("yyyy-MM-dd HH:mm:ss") ?? ""
            ));
        }

        sw.Stop();
        _logger.LogInformation("Exportados {Count} pedidos en {Ms} ms", pedidos.Count, sw.ElapsedMilliseconds);
        return (Encoding.UTF8.GetBytes(sb.ToString()), $"pedidos_{DateTime.Now:yyyyMMdd_HHmm}.csv");
    }

    // ── Exportar Clientes ─────────────────────────────────────────
    public async Task<(byte[] Contenido, string NombreArchivo)> ExportarClientesAsync()
    {
        var sw = Stopwatch.StartNew();
        var clientes = await _context.Customers
            .OrderBy(c => c.CreadoEn)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("tipo_cliente,nombre,apellido,telefono,correo,sexo,fecha_nacimiento,rfc,razon_social,cp_fiscal,regimen_fiscal,creado_en");

        foreach (var c in clientes)
        {
            sb.AppendLine(string.Join(",",
                Escapar(c.TipoCliente),
                Escapar(c.Nombre),
                Escapar(c.Apellido ?? ""),
                Escapar(c.Telefono),
                Escapar(c.Correo ?? ""),
                Escapar(c.Sexo ?? ""),
                c.FechaNacimiento?.ToString("yyyy-MM-dd") ?? "",
                Escapar(c.Rfc ?? ""),
                Escapar(c.RazonSocial ?? ""),
                Escapar(c.CpFiscal ?? ""),
                Escapar(c.RegimenFiscal ?? ""),
                c.CreadoEn.ToString("yyyy-MM-dd HH:mm:ss")
            ));
        }

        sw.Stop();
        _logger.LogInformation("Exportados {Count} clientes en {Ms} ms", clientes.Count, sw.ElapsedMilliseconds);
        return (Encoding.UTF8.GetBytes(sb.ToString()), $"clientes_{DateTime.Now:yyyyMMdd_HHmm}.csv");
    }
}
