using FloreriaBautista.Services;
using FloreriaBautista.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FloreriaBautista.Tests;

/// <summary>
/// FIX-004 (inventario-disponibilidad-ventas-prediccion): el corte del ciclo de abastecimiento se
/// entrega como instante UTC real, no como hora local marcada como UTC.
/// </summary>
public class AvailabilityCorteUtcTests
{
    // Martes 2026-10-20 12:00 hora de tienda (UTC-6). Valores por defecto: abastece lunes, corte sábado 12:00.
    private static readonly DateTime Ahora = new(2026, 10, 20, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Ciclo_entrega_el_corte_como_instante_utc_real()
    {
        using var ctx = new PedidosTestContext(Ahora);
        var svc = new AvailabilityService(ctx.Db, ctx.Fechas,
            new MemoryCache(new MemoryCacheOptions()), NullLogger<AvailabilityService>.Instance);

        var ciclo = await svc.ObtenerCicloAbastecimientoAsync();

        // Sábado 2026-10-24 12:00 en UTC-6 = 18:00 UTC.
        Assert.Equal(new DateTime(2026, 10, 24, 18, 0, 0, DateTimeKind.Utc), ciclo.Cutoff);
        Assert.Equal(DateTimeKind.Utc, ciclo.Cutoff.Kind);
        Assert.Equal("ABIERTO", ciclo.EstadoActual);
    }
}
