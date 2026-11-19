using FloreriaBautista.Models.DTOs.Inventory;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Services;
using FloreriaBautista.Services.Interfaces;
using FloreriaBautista.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FloreriaBautista.Tests;

/// <summary>
/// FIX-007 (inventario-disponibilidad-ventas-prediccion): la cantidad sugerida de la lista de
/// reabastecimiento no vuelve a restar el stock a una recomendación que ya viene neta de stock.
/// </summary>
public class InventoryReabastecimientoTests
{
    private static readonly DateTime Ahora = new(2026, 10, 20, 18, 0, 0, DateTimeKind.Utc);

    private sealed class ForecastFijo : IForecastService
    {
        private readonly Dictionary<Guid, ForecastResultDto> _porId;
        public ForecastFijo(Dictionary<Guid, ForecastResultDto> porId) => _porId = porId;
        public Task<ForecastResultDto> GenerarRecomendacionAsync(Guid id) => Task.FromResult(_porId[id]);
    }

    private static async Task<int> SugeridoAsync(int stock, int minimo, int recomendada)
    {
        using var ctx = new PedidosTestContext(Ahora);
        var insumo = new InventoryItem
        {
            Id = Guid.NewGuid(), Nombre = "Insumo", StockActual = stock,
            StockMinimo = minimo, Sucursal = "MATRIZ", UnidadMedida = "pza", Activo = true
        };
        ctx.Db.InventoryItems.Add(insumo);
        ctx.Db.InventoryMovements.Add(new InventoryMovement
        {
            Id = Guid.NewGuid(), InventoryItemId = insumo.Id, TipoMovimiento = "SALIDA", Cantidad = 1
        });
        await ctx.Db.SaveChangesAsync();

        var forecast = new ForecastFijo(new()
        {
            [insumo.Id] = new ForecastResultDto
            {
                InventoryItemId = insumo.Id, Nombre = insumo.Nombre, StockActual = stock,
                CantidadRecomendada = recomendada
            }
        });
        var svc = new InventoryService(ctx.Db, NullLogger<InventoryService>.Instance, null!,
            new MemoryCache(new MemoryCacheOptions()), null!, null!, forecast, null!);

        var lista = await svc.ObtenerReabastecimientoAsync(refresh: true);
        return Assert.Single(lista).CantidadSugerida;
    }

    [Fact]
    public async Task Sugerido_es_la_recomendacion_sin_restar_el_stock_otra_vez()
        => Assert.Equal(131, await SugeridoAsync(stock: 43, minimo: 10, recomendada: 131));

    [Fact]
    public async Task Sugerido_no_baja_a_cero_si_la_recomendacion_es_positiva()
        => Assert.Equal(4, await SugeridoAsync(stock: 36, minimo: 30, recomendada: 4));

    [Fact]
    public async Task Sugerido_sube_solo_lo_que_falta_para_el_minimo()
        => Assert.Equal(25, await SugeridoAsync(stock: 5, minimo: 30, recomendada: 4));

    [Fact]
    public async Task Sugerido_con_stock_negativo_no_suma_el_faltante_dos_veces()
        => Assert.Equal(125, await SugeridoAsync(stock: -40, minimo: 10, recomendada: 125));
}
