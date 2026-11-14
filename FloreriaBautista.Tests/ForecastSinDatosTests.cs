using FloreriaBautista.Models.Entities;
using FloreriaBautista.Services;
using FloreriaBautista.Services.Interfaces;
using FloreriaBautista.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FloreriaBautista.Tests;

/// <summary>
/// FIX-008 (inventario-disponibilidad-ventas-prediccion): un insumo sin historial de ventas
/// recomienda su stock mínimo configurado, no 0.
/// </summary>
public class ForecastSinDatosTests
{
    private static readonly DateTime Ahora = new(2026, 10, 20, 18, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(12, 4)]
    [InlineData(12, 0)]
    [InlineData(0, 5)]
    public async Task Sin_historial_recomienda_el_stock_minimo(int minimo, int stock)
    {
        using var ctx = new PedidosTestContext(Ahora);
        var insumo = new InventoryItem
        {
            Id = Guid.NewGuid(), Nombre = "Insumo sin ventas", StockActual = stock,
            StockMinimo = minimo, Sucursal = "MATRIZ", UnidadMedida = "pza", Activo = true
        };
        ctx.Db.InventoryItems.Add(insumo);
        await ctx.Db.SaveChangesAsync();

        var svc = new ForecastService(ctx.Db, ctx.Fechas, null!, null!,
            new MemoryCache(new MemoryCacheOptions()), NullLogger<ForecastService>.Instance);

        var r = await svc.GenerarRecomendacionAsync(insumo.Id);

        Assert.Equal("SIN_DATOS", r.NivelConfianza);
        Assert.Equal(minimo, r.CantidadRecomendada);
        Assert.Equal(minimo, r.RangoInferior);
        Assert.Equal(minimo, r.RangoSuperior);
        Assert.Equal("Sin historial suficiente, se sugiere mantener el stock mínimo configurado.", r.Explicacion);
    }
}
