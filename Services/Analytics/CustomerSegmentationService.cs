using Microsoft.EntityFrameworkCore;
using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Analytics;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Services.Interfaces;

namespace FloreriaBautista.Services.Analytics;

// Propuesta 3 (modelos predictivos) — segmentación de clientes por RFM (Recencia, Frecuencia,
// Monto) + demográficos (edad, sexo, colonia, categoría favorita, canal preferido, tasa de
// cancelación, antigüedad, regularidad de compra) + clustering (k-means, k=4). Se recalcula
// periódicamente y sobrescribe customer_segments. Ver modelos predictivos/propuestas.md
// (Propuesta 3) y estructura_datasets.md para el dataset de 17 columnas de referencia.
public class CustomerSegmentationService : ICustomerSegmentationService
{
    private const int K = 4;

    private readonly AppDbContext                          _context;
    private readonly IFechaHelper                          _fechas;
    private readonly ILogger<CustomerSegmentationService>  _logger;

    public CustomerSegmentationService(AppDbContext context, IFechaHelper fechas,
        ILogger<CustomerSegmentationService> logger)
    {
        _context = context;
        _fechas  = fechas;
        _logger  = logger;
    }

    private record RfmCliente(
        Guid CustomerId, string Nombre, string? Telefono, string? Correo,
        int RecenciaDias, int FrecuenciaPedidos, decimal MontoTotal,
        // Demográficos (importancia 2 en el dataset de la Propuesta 3):
        int? Edad, string? Sexo, string? Colonia, string? CategoriaFavorita, string? CanalPreferido,
        double TasaCancelacion, int AntiguedadDias, double? DiasPromedioEntrePedidos);

    public async Task<RecalcularSegmentosResultDto> RecalcularSegmentosAsync()
    {
        // Día de la tienda (ver IFechaHelper): recencia y edad se miden contra el
        // calendario local, no contra el UTC.
        var hoy = _fechas.AhoraLocal().Date;

        var crudo = await _context.Customers
            .Select(c => new
            {
                c.Id,
                Nombre = c.Nombre + (c.Apellido != null ? " " + c.Apellido : ""),
                c.Telefono,
                c.Correo,
                c.Sexo,
                c.FechaNacimiento,
                c.CreadoEn,
                Colonia = c.Addresses
                    .OrderByDescending(a => a.EsPrincipal)
                    .Select(a => (string?)a.Colonia)
                    .FirstOrDefault(),
                // Todos los pedidos (incluye cancelados) — se necesita el universo completo
                // para tasa_cancelacion y canal_preferido; el filtro != CANCELADO se aplica
                // después, en memoria, para RFM (igual que antes).
                Pedidos = c.Orders
                    .Select(o => new
                    {
                        o.FechaCreacion,
                        o.Total,
                        o.EstadoPedido,
                        o.Canal,
                        Categorias = o.OrderItems
                            .SelectMany(oi => oi.Product.ProductCategories.Select(pc => pc.Category.Nombre))
                    })
                    .ToList()
            })
            .Where(c => c.Pedidos.Any(p => p.EstadoPedido != "CANCELADO"))
            .ToListAsync();

        var clientes = crudo.Select(c =>
        {
            var pedidosValidos = c.Pedidos.Where(p => p.EstadoPedido != "CANCELADO").ToList();

            var edad = c.FechaNacimiento.HasValue ? CalcularEdad(c.FechaNacimiento.Value, hoy) : (int?)null;

            var canalPreferido = c.Pedidos
                .GroupBy(p => p.Canal)
                .OrderByDescending(g => g.Count())
                .Select(g => (string?)g.Key)
                .FirstOrDefault();

            var categoriaFavorita = pedidosValidos
                .SelectMany(p => p.Categorias)
                .GroupBy(cat => cat)
                .OrderByDescending(g => g.Count())
                .Select(g => (string?)g.Key)
                .FirstOrDefault();

            var tasaCancelacion = c.Pedidos.Count > 0
                ? (double)c.Pedidos.Count(p => p.EstadoPedido == "CANCELADO") / c.Pedidos.Count
                : 0.0;

            double? diasPromedioEntrePedidos = null;
            if (pedidosValidos.Count > 1)
            {
                var fechasOrdenadas = pedidosValidos.Select(p => p.FechaCreacion).OrderBy(f => f).ToList();
                var brechas = new List<double>();
                for (var i = 1; i < fechasOrdenadas.Count; i++)
                    brechas.Add((fechasOrdenadas[i] - fechasOrdenadas[i - 1]).TotalDays);
                diasPromedioEntrePedidos = brechas.Average();
            }

            return new RfmCliente(
                c.Id, c.Nombre, c.Telefono, c.Correo,
                RecenciaDias:      (hoy - pedidosValidos.Max(p => p.FechaCreacion).Date).Days,
                FrecuenciaPedidos: pedidosValidos.Count,
                MontoTotal:        pedidosValidos.Sum(p => p.Total),
                Edad:              edad,
                Sexo:              c.Sexo,
                Colonia:           c.Colonia,
                CategoriaFavorita: categoriaFavorita,
                CanalPreferido:    canalPreferido,
                TasaCancelacion:   tasaCancelacion,
                AntiguedadDias:    (hoy - c.CreadoEn.Date).Days,
                DiasPromedioEntrePedidos: diasPromedioEntrePedidos
            );
        }).ToList();

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
                    Edad               = clientes[i].Edad,
                    Sexo               = clientes[i].Sexo,
                    Colonia            = clientes[i].Colonia,
                    CategoriaFavorita  = clientes[i].CategoriaFavorita,
                    CanalPreferido     = clientes[i].CanalPreferido,
                    TasaCancelacion    = clientes[i].TasaCancelacion,
                    AntiguedadDias     = clientes[i].AntiguedadDias,
                    DiasPromedioEntrePedidos = clientes[i].DiasPromedioEntrePedidos,
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

    private static int CalcularEdad(DateOnly nacimiento, DateTime hoy)
    {
        var hoyDate = DateOnly.FromDateTime(hoy);
        var edad = hoyDate.Year - nacimiento.Year;
        if (nacimiento > hoyDate.AddYears(-edad)) edad--;
        return edad;
    }

    private static string? Moda(IEnumerable<string?> valores) =>
        valores
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .GroupBy(v => v)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();

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
                EdadPromedio         = g.Any(s => s.Edad.HasValue) ? Math.Round(g.Where(s => s.Edad.HasValue).Average(s => s.Edad!.Value), 1) : null,
                TasaCancelacionPromedio  = Math.Round(g.Average(s => s.TasaCancelacion), 3),
                ColoniaMasComun          = Moda(g.Select(s => s.Colonia)),
                CategoriaFavoritaMasComun = Moda(g.Select(s => s.CategoriaFavorita)),
                CanalPreferidoMasComun   = Moda(g.Select(s => s.CanalPreferido)),
                Clientes = g.OrderBy(s => s.RecenciaDias).Select(s => new CustomerSegmentDetailDto
                {
                    CustomerId        = s.CustomerId,
                    Nombre            = s.Customer.Nombre + (s.Customer.Apellido != null ? " " + s.Customer.Apellido : ""),
                    Telefono          = s.Customer.Telefono,
                    Correo            = s.Customer.Correo,
                    RecenciaDias      = s.RecenciaDias,
                    FrecuenciaPedidos = s.FrecuenciaPedidos,
                    MontoTotal        = s.MontoTotal,
                    Edad              = s.Edad,
                    Sexo              = s.Sexo,
                    Colonia           = s.Colonia,
                    CategoriaFavorita = s.CategoriaFavorita,
                    CanalPreferido    = s.CanalPreferido,
                    TasaCancelacion   = s.TasaCancelacion,
                    AntiguedadDias    = s.AntiguedadDias,
                    DiasPromedioEntrePedidos = s.DiasPromedioEntrePedidos
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

    // K-means sobre features normalizadas (min-max) que combinan RFM + demográficas
    // (Propuesta 3, ver modelos predictivos/propuestas.md): RFM (recencia, frecuencia, monto)
    // conserva mayor peso en la distancia por ser el eje central de la segmentación
    // (importancia 3 en el dataset); antigüedad, regularidad de compra, tasa de cancelación,
    // edad, sexo, colonia, categoría favorita y canal preferido (importancia 2) suman señal
    // adicional sin diluir el criterio principal. Las categóricas (sexo/colonia/categoría/canal)
    // se codifican por frecuencia relativa dentro de la base de clientes, para no explotar la
    // dimensionalidad con un one-hot de muchas colonias distintas.
    // Inicialización determinística (percentiles por monto) para que el resultado sea estable
    // entre corridas con los mismos datos, sin depender de un generador aleatorio.
    private static string[] ClusterizarKMeans(List<RfmCliente> clientes, int k)
    {
        var n = clientes.Count;

        double[] Normalizar(double[] valores)
        {
            var min = valores.Min();
            var max = valores.Max();
            var rango = max - min;
            return rango > 0 ? valores.Select(v => (v - min) / rango).ToArray() : valores.Select(_ => 0.0).ToArray();
        }

        double[] CodificarPorFrecuencia(IReadOnlyList<string?> valores)
        {
            var conteos = valores
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .GroupBy(v => v!)
                .ToDictionary(g => g.Key, g => (double)g.Count() / valores.Count);
            return valores.Select(v => v != null && conteos.TryGetValue(v, out var f) ? f : 0.0).ToArray();
        }

        // --- Importancia 3: RFM ---
        var recenciaN   = Normalizar(clientes.Select(c => (double)c.RecenciaDias).ToArray());
        var frecuenciaN = Normalizar(clientes.Select(c => (double)c.FrecuenciaPedidos).ToArray());
        var montoN      = Normalizar(clientes.Select(c => (double)c.MontoTotal).ToArray());

        // --- Importancia 2: demográficas ---
        var edadPromedioConocida = clientes.Where(c => c.Edad.HasValue).Select(c => (double)c.Edad!.Value).DefaultIfEmpty(0).Average();
        var edadN = Normalizar(clientes.Select(c => c.Edad.HasValue ? (double)c.Edad.Value : edadPromedioConocida).ToArray());

        var antiguedadN = Normalizar(clientes.Select(c => (double)c.AntiguedadDias).ToArray());

        // Clientes con un solo pedido no tienen "regularidad" — se tratan como el caso más
        // irregular observado, en vez de imputar 0 (que los haría ver artificialmente predecibles).
        var diasPromedioMax = clientes.Where(c => c.DiasPromedioEntrePedidos.HasValue)
            .Select(c => c.DiasPromedioEntrePedidos!.Value).DefaultIfEmpty(0).Max();
        var diasPromedioN = Normalizar(clientes.Select(c => c.DiasPromedioEntrePedidos ?? diasPromedioMax).ToArray());

        var tasaCancelacionN = Normalizar(clientes.Select(c => c.TasaCancelacion).ToArray());

        var sexoN             = CodificarPorFrecuencia(clientes.Select(c => c.Sexo).ToList());
        var colonaN           = CodificarPorFrecuencia(clientes.Select(c => c.Colonia).ToList());
        var categoriaFavoritaN = CodificarPorFrecuencia(clientes.Select(c => c.CategoriaFavorita).ToList());
        var canalPreferidoN   = CodificarPorFrecuencia(clientes.Select(c => c.CanalPreferido).ToList());

        const double pesoRfm  = 1.5; // importancia 3
        const double pesoDemo = 0.5; // importancia 2
        var wRfm  = Math.Sqrt(pesoRfm);
        var wDemo = Math.Sqrt(pesoDemo);

        // Los índices 0/1/2 (recencia/frecuencia/monto) se mantienen fijos porque el
        // etiquetado de negocio más abajo depende de ellos por posición.
        var puntos = Enumerable.Range(0, n)
            .Select(i => new[]
            {
                recenciaN[i]   * wRfm,
                frecuenciaN[i] * wRfm,
                montoN[i]      * wRfm,
                antiguedadN[i]          * wDemo,
                diasPromedioN[i]        * wDemo,
                tasaCancelacionN[i]     * wDemo,
                edadN[i]                * wDemo,
                sexoN[i]                * wDemo,
                colonaN[i]              * wDemo,
                categoriaFavoritaN[i]   * wDemo,
                canalPreferidoN[i]      * wDemo
            })
            .ToArray();

        var dim = puntos[0].Length;

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
                var nuevoCentroide = new double[dim];
                for (var d = 0; d < dim; d++)
                    nuevoCentroide[d] = miembros.Average(i => puntos[i][d]);
                centroides[c] = nuevoCentroide;
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
