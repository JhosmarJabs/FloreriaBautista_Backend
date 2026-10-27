using FloreriaBautista.Models.DTOs.Promotions;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Services;
using FloreriaBautista.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace FloreriaBautista.Tests;

/// <summary>FIX-007: mensaje único de cupón no válido y límite de 10 fallos por usuario.</summary>
public class CuponIntentosTests
{
    private static readonly DateTime Ahora = new(2026, 10, 20, 18, 0, 0, DateTimeKind.Utc);
    private const string Unico = "El código de cupón no es válido.";

    private static (PricingService Svc, PedidosTestContext Ctx, List<PricingItemDto> Items) Crear()
    {
        var ctx = new PedidosTestContext(Ahora);
        var prod = new Product { Id = Guid.NewGuid(), Nombre = "Rosa", PrecioBase = 100m, Estado = "ACTIVO" };
        ctx.Db.Products.Add(prod);
        ctx.Db.Promotions.AddRange(
            new Promotion { Id = Guid.NewGuid(), Nombre = "ok", Codigo = "BUENO", Valor = 10, Estado = "ACTIVO" },
            new Promotion { Id = Guid.NewGuid(), Nombre = "off", Codigo = "APAGADO", Valor = 10, Estado = "INACTIVO" },
            new Promotion { Id = Guid.NewGuid(), Nombre = "old", Codigo = "VENCIDO", Valor = 10, Estado = "ACTIVO",
                FechaFin = new DateOnly(2020, 1, 1) },
            new Promotion { Id = Guid.NewGuid(), Nombre = "fut", Codigo = "FUTURO", Valor = 10, Estado = "ACTIVO",
                FechaInicio = new DateOnly(2090, 1, 1) });
        ctx.Db.SaveChanges();
        var limiter = new CuponIntentosLimiter(new MemoryCache(new MemoryCacheOptions()));
        var items = new List<PricingItemDto> { new() { ProductId = prod.Id, Cantidad = 1 } };
        return (new PricingService(ctx.Db, limiter), ctx, items);
    }

    [Theory]
    [InlineData("NOEXISTE")]
    [InlineData("APAGADO")]
    [InlineData("VENCIDO")]
    [InlineData("FUTURO")]
    public async Task CuponNoValido_DaMensajeUnico(string codigo)
    {
        var (svc, ctx, items) = Crear();
        using var _ = ctx;
        var ex = await Assert.ThrowsAsync<AppException>(
            () => svc.CalcularTotalAsync(items, codigo, Guid.NewGuid()));
        Assert.Equal(Unico, ex.Message);
        Assert.IsNotType<TooManyRequestsException>(ex);
    }

    [Fact]
    public async Task Intento11_DaTooManyRequests_AunConCodigoValido_YNoAfectaAOtroUsuario()
    {
        var (svc, ctx, items) = Crear();
        using var _ = ctx;
        var usuario = Guid.NewGuid();

        for (var i = 0; i < 10; i++)
        {
            var ex = await Assert.ThrowsAsync<AppException>(
                () => svc.CalcularTotalAsync(items, "NOEXISTE", usuario));
            Assert.Equal(Unico, ex.Message);
            Assert.IsNotType<TooManyRequestsException>(ex);
        }

        await Assert.ThrowsAsync<TooManyRequestsException>(
            () => svc.CalcularTotalAsync(items, "BUENO", usuario));

        var otro = await svc.CalcularTotalAsync(items, "BUENO", Guid.NewGuid());
        Assert.NotNull(otro);
    }

    [Fact]
    public async Task CuponValido_NoCuentaComoFallo()
    {
        var (svc, ctx, items) = Crear();
        using var _ = ctx;
        var usuario = Guid.NewGuid();
        for (var i = 0; i < 9; i++)
            await Assert.ThrowsAsync<AppException>(() => svc.CalcularTotalAsync(items, "NOEXISTE", usuario));
        for (var i = 0; i < 20; i++)
            await svc.CalcularTotalAsync(items, "BUENO", usuario);
        await Assert.ThrowsAsync<AppException>(() => svc.CalcularTotalAsync(items, "NOEXISTE", usuario));
    }
}
