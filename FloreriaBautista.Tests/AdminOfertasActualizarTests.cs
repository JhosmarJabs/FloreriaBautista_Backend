using FloreriaBautista.Controllers.Admin;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Promotions;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Tests.TestHelpers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FloreriaBautista.Tests;

/// <summary>FIX-008: Actualizar oferta busca y asigna el producto nuevo (BUG-045, BUG-046).</summary>
public class AdminOfertasActualizarTests
{
    private static readonly DateTime Ahora = new(2026, 10, 25, 18, 0, 0, DateTimeKind.Utc);

    private static (AdminOfertasController Ctrl, Oferta Oferta, Product Nuevo) Preparar()
    {
        var ctx = new PedidosTestContext(Ahora);
        var a = new Product { Id = Guid.NewGuid(), Nombre = "P-A", PrecioBase = 100m, Estado = "ACTIVO" };
        var b = new Product { Id = Guid.NewGuid(), Nombre = "P-B", PrecioBase = 250m, Estado = "ACTIVO" };
        var of = new Oferta { Id = Guid.NewGuid(), ProductoId = a.Id, PrecioOferta = 80m, Activo = true };
        ctx.Db.Products.AddRange(a, b);
        ctx.Db.Ofertas.Add(of);
        ctx.Db.SaveChanges();
        return (new AdminOfertasController(ctx.Db), of, b);
    }

    [Fact]
    public async Task ProductoInexistente_Da400_YNoTocaLaOferta()
    {
        var (ctrl, of, _) = Preparar();
        var original = of.ProductoId;
        var r = await ctrl.Actualizar(of.Id, new SaveOfertaRequestDto { ProductoId = Guid.NewGuid(), PrecioOferta = 90m, Activo = true });
        var bad = Assert.IsType<BadRequestObjectResult>(r);
        Assert.Equal("Producto no encontrado.", Assert.IsType<ApiResponseDto<object>>(bad.Value).Message);
        Assert.Equal(original, of.ProductoId);
        Assert.Equal(80m, of.PrecioOferta);
    }

    [Fact]
    public async Task ProductoNuevo_RespondeConNombreYPrecioDelNuevo()
    {
        var (ctrl, of, nuevo) = Preparar();
        var r = await ctrl.Actualizar(of.Id, new SaveOfertaRequestDto { ProductoId = nuevo.Id, PrecioOferta = 200m, Activo = true });
        var dto = Assert.IsType<ApiResponseDto<OfertaDto>>(Assert.IsType<OkObjectResult>(r).Value).Data!;
        Assert.Equal("P-B", dto.ProductoNombre);
        Assert.Equal(250m, dto.PrecioBase);
        Assert.Equal(nuevo.Id, dto.ProductoId);
    }

    [Fact]
    public async Task MismoProducto_SigueActualizandoCampos()
    {
        var (ctrl, of, _) = Preparar();
        var r = await ctrl.Actualizar(of.Id, new SaveOfertaRequestDto { ProductoId = of.ProductoId, PrecioOferta = 70m, Activo = true });
        var dto = Assert.IsType<ApiResponseDto<OfertaDto>>(Assert.IsType<OkObjectResult>(r).Value).Data!;
        Assert.Equal(70m, dto.PrecioOferta);
        Assert.Equal("P-A", dto.ProductoNombre);
    }
}
