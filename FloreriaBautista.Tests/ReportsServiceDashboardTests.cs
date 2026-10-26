using FloreriaBautista.Data;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Services;
using FloreriaBautista.Services.Reports;
using FloreriaBautista.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace FloreriaBautista.Tests;

/// <summary>
/// BATCH-11 (pruebas-administrador): ReportsService.ObtenerDashboardStatsAsync.
/// Reloj fijo 2026-10-20 18:00 UTC = 12:00 en la tienda (UTC-6). Base en memoria por prueba.
/// </summary>
public class ReportsServiceDashboardTests
{
    private static readonly DateTime AhoraUtc = new(2026, 10, 20, 18, 0, 0, DateTimeKind.Utc);
    private readonly ITestOutputHelper _out;

    public ReportsServiceDashboardTests(ITestOutputHelper output) => _out = output;

    private static AppDbContext NuevaBd() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"dashboard-{Guid.NewGuid()}").Options);

    private static ReportsService Servicio(AppDbContext db) =>
        new(db, new FechaHelper(PedidosTestContext.ZonaTienda, () => AhoraUtc),
            NullLogger<ReportsService>.Instance);

    private static InventoryItem Insumo(string nombre, int actual, int minimo, bool activo = true) => new()
    {
        Id = Guid.NewGuid(), Nombre = nombre, Sucursal = "PRINCIPAL",
        StockActual = actual, StockMinimo = minimo, Activo = activo
    };

    [Fact]
    public async Task TotalReal_Con25Criticos_Devuelve25YLista10()
    {
        using var db = NuevaBd();
        for (var i = 0; i < 25; i++) db.InventoryItems.Add(Insumo($"QA-C{i}", i * 2, 50));
        for (var i = 0; i < 5; i++) db.InventoryItems.Add(Insumo($"QA-OK{i}", 60, 10));
        db.SaveChanges();

        var stats = await Servicio(db).ObtenerDashboardStatsAsync("mes");

        Assert.Equal(25, stats.CriticalInventoryTotal);
        Assert.Equal(10, stats.CriticalInventory.Count);
        Assert.Equal(Enumerable.Range(0, 10).Select(i => i * 2), stats.CriticalInventory.Select(x => x.StockActual));
        Assert.DoesNotContain(stats.CriticalInventory, x => x.StockActual == 60);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(10, 10)]
    [InlineData(11, 10)]
    public async Task Limites_TotalYTamanoDeLista(int n, int esperadoLista)
    {
        using var db = NuevaBd();
        for (var i = 0; i < n; i++) db.InventoryItems.Add(Insumo($"QA-C{i}", i, 20));
        for (var i = 0; i < 3; i++) db.InventoryItems.Add(Insumo($"QA-OK{i}", 100, 20));
        db.SaveChanges();

        var stats = await Servicio(db).ObtenerDashboardStatsAsync("mes");

        Assert.Equal(n, stats.CriticalInventoryTotal);
        Assert.Equal(esperadoLista, stats.CriticalInventory.Count);
        if (n == 0)
        {
            Assert.NotNull(stats.CriticalInventory);
            Assert.Empty(stats.CriticalInventory);
        }
    }

    [Fact]
    public async Task Predicado_IgualdadInactivosYCero()
    {
        using var db = NuevaBd();
        db.InventoryItems.AddRange(
            Insumo("QA-IGUAL", 5, 5), Insumo("QA-DEBAJO", 4, 5), Insumo("QA-ARRIBA", 6, 5),
            Insumo("QA-CERO", 0, 0),
            Insumo("QA-INACTIVO-1", 0, 10, activo: false), Insumo("QA-INACTIVO-2", 3, 10, activo: false));
        db.SaveChanges();

        var inactivosGuardados = db.InventoryItems.AsNoTracking().Count(i => !i.Activo);
        _out.WriteLine($"NOTA-PASO-1: inactivos guardados en la base en memoria = {inactivosGuardados} (se sembraron 2)");

        var stats = await Servicio(db).ObtenerDashboardStatsAsync("mes");

        // La igualdad, el mínimo cero y el insumo por encima del mínimo se afirman siempre.
        Assert.Contains(stats.CriticalInventory, x => x.Nombre == "QA-IGUAL");
        Assert.Contains(stats.CriticalInventory, x => x.Nombre == "QA-DEBAJO");
        Assert.Contains(stats.CriticalInventory, x => x.Nombre == "QA-CERO");
        Assert.DoesNotContain(stats.CriticalInventory, x => x.Nombre == "QA-ARRIBA");

        // La aserción de inactivos solo es verificable si la base los guardó como inactivos (trampa HasDefaultValue).
        Assert.Equal(2, inactivosGuardados);
        Assert.Equal(3, stats.CriticalInventoryTotal);
        Assert.Equal(new[] { "QA-CERO", "QA-DEBAJO", "QA-IGUAL" },
            stats.CriticalInventory.Select(x => x.Nombre).OrderBy(n => n).ToArray());
        Assert.DoesNotContain(stats.CriticalInventory, x => x.Nombre.StartsWith("QA-INACTIVO"));
    }

    [Theory]
    [InlineData("dia", 1, 100, 1)]
    [InlineData("semana", 7, 300, 2)]
    [InlineData("mes", 30, 700, 3)]
    public async Task Periodo_NoAfectaCriticos_SoloVentas(string periodo, int diasGrafica, int totalEsperado, int pedidosEsperados)
    {
        using var ctx = new PedidosTestContext(AhoraUtc);
        var db = ctx.Db;
        for (var i = 0; i < 12; i++) db.InventoryItems.Add(Insumo($"QA-C{i}", i, 20));
        db.SaveChanges();

        ctx.AgregarPedido(new DateOnly(2026, 10, 25), "CONFIRMADO", fechaCreacion: AhoraUtc, total: 100m);
        ctx.AgregarPedido(new DateOnly(2026, 10, 25), "CONFIRMADO", fechaCreacion: AhoraUtc.AddDays(-3), total: 200m);
        ctx.AgregarPedido(new DateOnly(2026, 10, 25), "CONFIRMADO", fechaCreacion: AhoraUtc.AddDays(-20), total: 400m);
        ctx.AgregarPedido(new DateOnly(2026, 10, 25), "CANCELADO", fechaCreacion: AhoraUtc, total: 999m);

        var stats = await Servicio(db).ObtenerDashboardStatsAsync(periodo);

        Assert.Equal(12, stats.CriticalInventoryTotal);
        Assert.Equal(10, stats.CriticalInventory.Count);
        Assert.Equal(diasGrafica, stats.MonthlySales.Count);
        Assert.Equal(totalEsperado, stats.TotalSales);
        Assert.Equal(pedidosEsperados, stats.OrderCount);
        Assert.Equal(stats.TotalSales / stats.OrderCount, stats.AverageTicket);
    }
}
