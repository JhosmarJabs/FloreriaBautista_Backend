using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Tests.TestHelpers;
using Xunit;

namespace FloreriaBautista.Tests;

/// <summary>
/// Los tres listados que consulta la administradora (gastos, cortes y reportes
/// de error) acotan page y size igual que el resto del proyecto: page ≥ 1 y
/// 1 ≤ size ≤ 100. La base InMemory no reproduce el error de OFFSET negativo de
/// PostgreSQL, así que aquí se fijan los valores que devuelve el servicio.
/// </summary>
public class PaginacionSupervisionTests
{
    private static readonly DateTime Ahora = new(2026, 8, 12, 5, 0, 0, DateTimeKind.Utc);

    private const int Registros = 105;

    public enum Listado { Gastos, Cortes, Reportes }

    private sealed record Pagina(int Elementos, int Total, int Numero, int Tamano, int TotalPaginas, Guid? Primero);

    /// <summary>Contexto con <see cref="Registros"/> registros del listado indicado.</summary>
    private static PedidosTestContext Sembrar(Listado listado)
    {
        var ctx = new PedidosTestContext(Ahora);
        var ana = ctx.AgregarEmpleado("Ana");

        for (var i = 0; i < Registros; i++)
        {
            var momento = Ahora.AddMinutes(-i);
            switch (listado)
            {
                case Listado.Gastos:
                    ctx.AgregarGasto(ana, momento, 10m);
                    break;
                case Listado.Cortes:
                    ctx.Db.CashCuts.Add(new CashCut
                    {
                        Id              = Guid.NewGuid(),
                        UsuarioId       = ana,
                        FechaLocal      = new DateOnly(2026, 8, 11).AddDays(-i),
                        FechaHoraCierre = momento,
                        TotalVentas     = 10m
                    });
                    break;
                case Listado.Reportes:
                    ctx.Db.ErrorReports.Add(new ErrorReport
                    {
                        Id           = Guid.NewGuid(),
                        UsuarioId    = ana,
                        TipoRegistro = "GASTO",
                        RegistroId   = Guid.NewGuid(),
                        Motivo       = "Importe equivocado",
                        FechaHora    = momento
                    });
                    break;
            }
        }
        ctx.Db.SaveChanges();
        return ctx;
    }

    private static async Task<Pagina> ListarAsync(PedidosTestContext ctx, Listado listado, int page, int size)
    {
        switch (listado)
        {
            case Listado.Gastos:
                return Resumir(await ctx.CrearGastoService().ListarAdminAsync(null, null, null, page, size), g => g.Id);
            case Listado.Cortes:
                return Resumir(await ctx.CrearCorteService().ListarAdminAsync(null, null, null, page, size), c => c.Id);
            default:
                return Resumir(await ctx.CrearReporteService().ListarAdminAsync(null, page, size), r => r.Id);
        }
    }

    private static Pagina Resumir<T>(PagedResultDto<T> r, Func<T, Guid> id) =>
        new(r.Items.Count, r.Total, r.Pagina, r.TamanoPagina, r.TotalPaginas,
            r.Items.Count == 0 ? null : id(r.Items[0]));

    [Theory]
    [InlineData(Listado.Gastos)]
    [InlineData(Listado.Cortes)]
    [InlineData(Listado.Reportes)]
    public async Task Un_Size_Mayor_A_100_Se_Acota_A_100(Listado listado)
    {
        using var ctx = Sembrar(listado);

        var pagina = await ListarAsync(ctx, listado, page: 1, size: 500);

        Assert.Equal(100, pagina.Tamano);
        Assert.Equal(100, pagina.Elementos);
        Assert.Equal(Registros, pagina.Total);
        Assert.Equal(2, pagina.TotalPaginas);
    }

    [Theory]
    [InlineData(Listado.Gastos,   0)]
    [InlineData(Listado.Gastos,  -5)]
    [InlineData(Listado.Cortes,   0)]
    [InlineData(Listado.Cortes,  -5)]
    [InlineData(Listado.Reportes, 0)]
    [InlineData(Listado.Reportes, -5)]
    public async Task Un_Size_Cero_O_Negativo_Se_Acota_A_1(Listado listado, int size)
    {
        using var ctx = Sembrar(listado);

        var pagina = await ListarAsync(ctx, listado, page: 1, size: size);

        Assert.Equal(1, pagina.Tamano);
        Assert.Equal(1, pagina.Elementos);
        Assert.Equal(pagina.Total, pagina.TotalPaginas);
    }

    [Theory]
    [InlineData(Listado.Gastos,   0)]
    [InlineData(Listado.Gastos,  -1)]
    [InlineData(Listado.Cortes,   0)]
    [InlineData(Listado.Cortes,  -1)]
    [InlineData(Listado.Reportes, 0)]
    [InlineData(Listado.Reportes, -1)]
    public async Task Una_Page_Cero_O_Negativa_Devuelve_La_Primera_Pagina(Listado listado, int page)
    {
        using var ctx = Sembrar(listado);

        var primera = await ListarAsync(ctx, listado, page: 1, size: 20);
        var pagina  = await ListarAsync(ctx, listado, page: page, size: 20);

        Assert.Equal(1, pagina.Numero);
        Assert.Equal(20, pagina.Elementos);
        Assert.Equal(primera.Primero, pagina.Primero);
    }

    [Theory]
    [InlineData(Listado.Gastos)]
    [InlineData(Listado.Cortes)]
    [InlineData(Listado.Reportes)]
    public async Task Un_Size_Valido_No_Cambia(Listado listado)
    {
        using var ctx = Sembrar(listado);

        var pagina = await ListarAsync(ctx, listado, page: 2, size: 20);

        Assert.Equal(2, pagina.Numero);
        Assert.Equal(20, pagina.Tamano);
        Assert.Equal(20, pagina.Elementos);
        Assert.Equal(Registros, pagina.Total);
        Assert.Equal(6, pagina.TotalPaginas);
    }
}
