using Microsoft.EntityFrameworkCore;
using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Analytics;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Services.Interfaces;

namespace FloreriaBautista.Services.Analytics;

// Propuesta 3 (modelos predictivos) — segmentación de clientes por RFM (Recencia, Frecuencia,
// Monto) + clustering (k-means, k=4). Se recalcula periódicamente y sobrescribe customer_segments.
public class CustomerSegmentationService : ICustomerSegmentationService
{
    private const int K = 4;

    private readonly AppDbContext                          _context;
    private readonly ILogger<CustomerSegmentationService>  _logger;

    public CustomerSegmentationService(AppDbContext context, ILogger<CustomerSegmentationService> logger)
    {
        _context = context;
        _logger  = logger;
    }

    private record RfmCliente(Guid CustomerId, string Nombre, string? Telefono, string? Correo,
        int RecenciaDias, int FrecuenciaPedidos, decimal MontoTotal);

    public async Task<RecalcularSegmentosResultDto> RecalcularSegmentosAsync()
    {
        var hoy = DateTime.UtcNow.Date;

        var crudo = await _context.Customers
            .Select(c => new
            {
                c.Id,
                Nombre = c.Nombre + (c.Apellido != null ? " " + c.Apellido : ""),
                c.Telefono,
                c.Correo,
                Pedidos = c.Orders
                    .Where(o => o.EstadoPedido != "CANCELADO")
                    .Select(o => new { o.FechaCreacion, o.Total })
                    .ToList()
            })
            .Where(c => c.Pedidos.Count > 0)
            .ToListAsync();

        var clientes = crudo.Select(c => new RfmCliente(
            c.Id, c.Nombre, c.Telefono, c.Correo,
            RecenciaDias:      (hoy - c.Pedidos.Max(p => p.FechaCreacion).Date).Days,
            FrecuenciaPedidos: c.Pedidos.Count,
            MontoTotal:        c.Pedidos.Sum(p => p.Total)
        )).ToList();

        var segmentosNuevos = new List<CustomerSegment>();

        if (clientes.Count > 0)
        {
            var k = Math.Min(K, clientes.Count);
            var etiquetas = ClusterizarKMeans(clientes, k);

            var fechaCalculo = DateTime.UtcNow;
            for (var i = 0; i < clientes.Count; i++)
            {
                segmentosNuevos.Add(new CustomerSegment
                {
                    Id                 = Guid.NewGuid(),
                    CustomerId         = clientes[i].CustomerId,
                    Grupo              = etiquetas[i],
                    RecenciaDias       = clientes[i].RecenciaDias,
                    FrecuenciaPedidos  = clientes[i].FrecuenciaPedidos,
                    MontoTotal         = clientes[i].MontoTotal,
                    FechaCalculo       = fechaCalculo
                });
            }
        }

        _context.CustomerSegments.RemoveRange(_context.CustomerSegments);
        if (segmentosNuevos.Count > 0)
            await _context.CustomerSegments.AddRangeAsync(segmentosNuevos);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Segmentación de clientes recalculada: {Clientes} clientes procesados.", clientes.Count);

        return new RecalcularSegmentosResultDto
        {
            ClientesProcesados = clientes.Count,
            GruposGenerados    = segmentosNuevos.Select(s => s.Grupo).Distinct().Count(),
            CalculadoEn        = DateTime.UtcNow
        };
    }

    public async Task<List<CustomerSegmentGroupDto>> ObtenerSegmentosAsync()
    {
        var segmentos = await _context.CustomerSegments
            .Include(s => s.Customer)
            .ToListAsync();

        return segmentos
            .GroupBy(s => s.Grupo)
            .Select(g => new CustomerSegmentGroupDto
            {
                Grupo                = g.Key,
                TotalClientes        = g.Count(),
                MontoTotalGrupo      = g.Sum(s => s.MontoTotal),
                MontoPromedio        = Math.Round(g.Average(s => s.MontoTotal), 2),
                RecenciaPromedioDias = Math.Round(g.Average(s => s.RecenciaDias), 1),
                FrecuenciaPromedio   = Math.Round(g.Average(s => s.FrecuenciaPedidos), 1),
                Clientes = g.OrderBy(s => s.RecenciaDias).Select(s => new CustomerSegmentDetailDto
                {
                    CustomerId        = s.CustomerId,
                    Nombre            = s.Customer.Nombre + (s.Customer.Apellido != null ? " " + s.Customer.Apellido : ""),
                    Telefono          = s.Customer.Telefono,
                    Correo            = s.Customer.Correo,
                    RecenciaDias      = s.RecenciaDias,
                    FrecuenciaPedidos = s.FrecuenciaPedidos,
                    MontoTotal        = s.MontoTotal
                }).ToList()
            })
            .OrderBy(g => OrdenGrupo(g.Grupo))
            .ToList();
    }

    private static int OrdenGrupo(string grupo) => grupo switch
    {
        "VIP"       => 0,
        "FRECUENTE" => 1,
        "OCASIONAL" => 2,
        "INACTIVO"  => 3,
        _           => 4
    };

    // K-means simple sobre 3 features normalizadas (min-max): recencia, frecuencia, monto.
    // Inicialización determinística (percentiles por monto) para que el resultado sea estable
    // entre corridas con los mismos datos, sin depender de un generador aleatorio.
    private static string[] ClusterizarKMeans(List<RfmCliente> clientes, int k)
    {
        var n = clientes.Count;
        var recencia   = clientes.Select(c => (double)c.RecenciaDias).ToArray();
        var frecuencia = clientes.Select(c => (double)c.FrecuenciaPedidos).ToArray();
        var monto      = clientes.Select(c => (double)c.MontoTotal).ToArray();

        double[] Normalizar(double[] valores)
        {
            var min = valores.Min();
            var max = valores.Max();
            var rango = max - min;
            return rango > 0 ? valores.Select(v => (v - min) / rango).ToArray() : valores.Select(_ => 0.0).ToArray();
        }

        var recenciaN   = Normalizar(recencia);
        var frecuenciaN = Normalizar(frecuencia);
        var montoN      = Normalizar(monto);

        var puntos = Enumerable.Range(0, n)
            .Select(i => new[] { recenciaN[i], frecuenciaN[i], montoN[i] })
            .ToArray();

        // Inicialización: ordenar por monto (desc) y tomar puntos en percentiles equiespaciados.
        var ordenPorMonto = Enumerable.Range(0, n).OrderByDescending(i => montoN[i]).ToArray();
        var centroides = new double[k][];
        for (var c = 0; c < k; c++)
        {
            var idx = ordenPorMonto[(int)((c + 0.5) * n / k)];
            centroides[c] = (double[])puntos[idx].Clone();
        }

        var asignacion = new int[n];
        for (var iter = 0; iter < 50; iter++)
        {
            var cambios = false;

            for (var i = 0; i < n; i++)
            {
                var mejor = 0;
                var mejorDist = DistanciaCuadrada(puntos[i], centroides[0]);
                for (var c = 1; c < k; c++)
                {
                    var dist = DistanciaCuadrada(puntos[i], centroides[c]);
                    if (dist < mejorDist) { mejorDist = dist; mejor = c; }
                }
                if (asignacion[i] != mejor) cambios = true;
                asignacion[i] = mejor;
            }

            for (var c = 0; c < k; c++)
            {
                var miembros = Enumerable.Range(0, n).Where(i => asignacion[i] == c).ToList();
                if (miembros.Count == 0) continue; // conservar el centroide anterior si el clúster quedó vacío
                centroides[c] = [
                    miembros.Average(i => puntos[i][0]),
                    miembros.Average(i => puntos[i][1]),
                    miembros.Average(i => puntos[i][2])
                ];
            }

            if (!cambios) break;
        }

        // Etiquetado de negocio a partir de las características de cada centroide final:
        // 1) el clúster con mayor recencia promedio (más días sin comprar) → INACTIVO
        // 2) de los restantes, el de mayor monto promedio → VIP
        // 3) de los restantes, el de mayor frecuencia promedio → FRECUENTE
        // 4) el que queda → OCASIONAL
        var pendientes = Enumerable.Range(0, k).ToList();
        var etiquetaPorClúster = new string[k];

        var inactivo = pendientes.OrderByDescending(c => centroides[c][0]).First();
        etiquetaPorClúster[inactivo] = "INACTIVO";
        pendientes.Remove(inactivo);

        if (pendientes.Count > 0)
        {
            var vip = pendientes.OrderByDescending(c => centroides[c][2]).First();
            etiquetaPorClúster[vip] = "VIP";
            pendientes.Remove(vip);
        }

        if (pendientes.Count > 0)
        {
            var frecuente = pendientes.OrderByDescending(c => centroides[c][1]).First();
            etiquetaPorClúster[frecuente] = "FRECUENTE";
            pendientes.Remove(frecuente);
        }

        foreach (var restante in pendientes)
            etiquetaPorClúster[restante] = "OCASIONAL";

        return asignacion.Select(c => etiquetaPorClúster[c]).ToArray();
    }

    private static double DistanciaCuadrada(double[] a, double[] b)
    {
        double suma = 0;
        for (var i = 0; i < a.Length; i++)
        {
            var diff = a[i] - b[i];
            suma += diff * diff;
        }
        return suma;
    }
}
