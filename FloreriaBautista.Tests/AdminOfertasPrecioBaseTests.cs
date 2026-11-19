using FloreriaBautista.Controllers.Admin;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Promotions;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Tests.TestHelpers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FloreriaBautista.Tests;

/// <summary>FIX-009 (V-03 = A): el precio de oferta debe ser menor al precio normal (BUG-047).</summary>
public class AdminOfertasPrecioBaseTests
{
    private static readonly DateTime Ahora = new(2026, 11, 6, 18, 0, 0, DateTimeKind.Utc);

    private static (AdminOfertasController Ctrl, Product A, Product B, Oferta OfertaB) Preparar()
    {
        var ctx = new PedidosTestContext(Ahora);
        var a = new Product { Id = Guid.NewGuid(), Nombre = "P-A", PrecioBase = 500m, Estado = "ACTIVO" };
        var b = new Product { Id = Guid.NewGuid(), Nombre = "P-B", PrecioBase = 300m, Estado = "ACTIVO" };
        var of = new Oferta { Id = Guid.NewGuid(), ProductoId = b.Id, PrecioOferta = 250m, Activo = true };
        ctx.Db.Products.AddRange(a, b);
        ctx.Db.Ofertas.Add(of);
        ctx.Db.SaveChanges();
        return (new AdminOfertasController(ctx.Db), a, b, of);
    }

    [Theory]
    [InlineData(300)]
    [InlineData(450)]
    public async Task Crear_PrecioIgualOMayorAlBase_Da400(int precio)
    {
        var (ctrl, a, _, _) = Preparar();
        var r = await ctrl.Crear(new SaveOfertaRequestDto { ProductoId = a.Id, PrecioOferta = precio == 300 ? 500m : 650m, Activo = true });
        Assert.IsType<BadRequestObjectResult>(r);
    }

    [Fact]
    public async Task Crear_PrecioMenor_Da200()
    {
        var (ctrl, a, _, _) = Preparar();
        var r = await ctrl.Crear(new SaveOfertaRequestDto { ProductoId = a.Id, PrecioOferta = 420m, Activo = true });
        Assert.IsType<OkObjectResult>(r);
    }

    [Theory]
    [InlineData(300)]
    [InlineData(450)]
    public async Task Actualizar_PrecioIgualOMayor_Da400_YNoTocaLaOferta(int precio)
    {
        var (ctrl, _, b, of) = Preparar();
        var r = await ctrl.Actualizar(of.Id, new SaveOfertaRequestDto { ProductoId = b.Id, PrecioOferta = precio, Activo = true });
        var bad = Assert.IsType<BadRequestObjectResult>(r);
        Assert.Contains("menor al precio normal", Assert.IsType<ApiResponseDto<object>>(bad.Value).Message);
        Assert.Equal(250m, of.PrecioOferta);
    }

    [Fact]
    public async Task Actualizar_CambioDeProducto_ComparaContraElNuevo()
    {
        var (ctrl, a, b, of) = Preparar();
        // 450 < 500 (P-A): válido aunque sería >= 300 (P-B).
        var ok = await ctrl.Actualizar(of.Id, new SaveOfertaRequestDto { ProductoId = a.Id, PrecioOferta = 450m, Activo = true });
        Assert.IsType<OkObjectResult>(ok);
        Assert.Equal(a.Id, of.ProductoId);
        // Volver a P-B con 450 (>= 300): 400 y sigue sobre P-A.
        var bad = await ctrl.Actualizar(of.Id, new SaveOfertaRequestDto { ProductoId = b.Id, PrecioOferta = 450m, Activo = true });
        Assert.IsType<BadRequestObjectResult>(bad);
        Assert.Equal(a.Id, of.ProductoId);
        Assert.Equal(450m, of.PrecioOferta);
    }
}
