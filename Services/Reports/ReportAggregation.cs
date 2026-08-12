namespace FloreriaBautista.Services.Reports;

using FloreriaBautista.Models.DTOs.Reports;

/// <summary>
/// Agregaciones compartidas por todos los reportes: series temporales con los
/// huecos rellenos y cortes por categoría con su participación.
///
/// Rellenar los días sin datos con cero no es inventar información: es decir que
/// ese día hubo cero. Sin eso el eje de fechas se comprime y una semana muerta
/// parece un bache de un solo día.
/// </summary>
public static class ReportAggregation
{
    /// <summary>
    /// Serie temporal continua sobre el periodo. `filas` aporta (fecha local,
    /// cantidad, monto); los buckets vacíos se emiten en cero.
    /// </summary>
    public static List<TimeBucketDto> Serie(
        ReportPeriod p,
        ReportGranularity granularidad,
        IEnumerable<(DateOnly Fecha, int Cantidad, decimal Monto)> filas)
    {
        var acumulado = new Dictionary<DateOnly, (int Cantidad, decimal Monto)>();

        foreach (var (fecha, cantidad, monto) in filas)
        {
            if (!p.Contiene(fecha)) continue;
            var clave = granularidad.InicioDeBucket(fecha);
            var actual = acumulado.GetValueOrDefault(clave);
            acumulado[clave] = (actual.Cantidad + cantidad, actual.Monto + monto);
        }

        var serie = new List<TimeBucketDto>();
        var cursor = granularidad.InicioDeBucket(p.Desde);

        while (cursor <= p.Hasta)
        {
            var siguiente = granularidad.SiguienteBucket(cursor);
            var valores   = acumulado.GetValueOrDefault(cursor);

            serie.Add(new TimeBucketDto
            {
                Etiqueta = granularidad.Etiqueta(cursor),
                // El primer y el último bucket se recortan al periodo pedido para
                // que la etiqueta no prometa días fuera del rango consultado.
                Inicio   = cursor < p.Desde ? p.Desde : cursor,
                Fin      = siguiente.AddDays(-1) > p.Hasta ? p.Hasta : siguiente.AddDays(-1),
                Cantidad = valores.Cantidad,
                Monto    = valores.Monto
            });

            cursor = siguiente;
        }

        return serie;
    }

    /// <summary>
    /// Corte por categoría ordenado por monto, con el porcentaje de participación
    /// sobre el total. Si todos los montos son cero, el porcentaje se reparte por
    /// número de ocurrencias en vez de quedar en cero (útil en cortes de conteo).
    /// </summary>
    public static List<BreakdownDto> Corte(IEnumerable<(string Clave, decimal Monto)> filas)
    {
        var grupos = filas
            .GroupBy(f => f.Clave)
            .Select(g => new BreakdownDto
            {
                Clave    = g.Key,
                Cantidad = g.Count(),
                Monto    = g.Sum(x => x.Monto)
            })
            .ToList();

        var totalMonto    = grupos.Sum(g => g.Monto);
        var totalCantidad = grupos.Sum(g => g.Cantidad);

        foreach (var g in grupos)
        {
            g.Porcentaje = totalMonto != 0
                ? Math.Round(g.Monto / totalMonto * 100, 1)
                : totalCantidad > 0 ? Math.Round((decimal)g.Cantidad / totalCantidad * 100, 1) : 0m;
        }

        return grupos.OrderByDescending(g => g.Monto).ThenByDescending(g => g.Cantidad).ToList();
    }

    /// Porcentaje seguro: null cuando el denominador es cero.
    public static decimal? Pct(decimal parte, decimal total) =>
        total == 0 ? null : Math.Round(parte / total * 100, 1);
}
