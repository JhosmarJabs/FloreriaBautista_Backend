using System.ComponentModel.DataAnnotations;
using FloreriaBautista.Controllers.Admin;
using FloreriaBautista.Models.DTOs.Inventory;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Services.Interfaces;
using FloreriaBautista.Tests.TestHelpers;
using Xunit;

namespace FloreriaBautista.Tests;

/// <summary>
/// FIX-006 (inventario-disponibilidad-ventas-prediccion): los endpoints de consulta
/// validate-instant y validate-advance rechazan cantidad fuera de 1..999, fecha omitida y fecha pasada.
/// </summary>
public class AdminAvailabilityValidacionTests
{
    // Martes 2026-10-20 12:00 hora de tienda (UTC-6).
    private static readonly DateTime Ahora = new(2026, 10, 20, 18, 0, 0, DateTimeKind.Utc);

    private static IList<ValidationResult> Validar(object modelo)
    {
        var resultados = new List<ValidationResult>();
        Validator.TryValidateObject(modelo, new ValidationContext(modelo), resultados, validateAllProperties: true);
        return resultados;
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    [InlineData(999, true)]
    [InlineData(1000, false)]
    public void Instantanea_valida_el_rango_de_cantidad(int cantidad, bool valido)
    {
        var r = Validar(new ValidateInstantRequest { ProductId = Guid.NewGuid(), Cantidad = cantidad });
        Assert.Equal(valido, r.Count == 0);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-5, false)]
    [InlineData(1, true)]
    [InlineData(1000, false)]
    public void Anticipada_valida_el_rango_de_cantidad(int cantidad, bool valido)
    {
        var r = Validar(new ValidateAdvanceRequest
        {
            ProductId = Guid.NewGuid(), Cantidad = cantidad, FechaEntrega = new DateOnly(2026, 10, 21)
        });
        Assert.Equal(valido, r.Count == 0);
    }

    [Fact]
    public void Anticipada_exige_fecha_de_entrega()
    {
        var r = Validar(new ValidateAdvanceRequest { ProductId = Guid.NewGuid(), Cantidad = 1, FechaEntrega = null });
        Assert.NotEmpty(r);
    }

    [Fact]
    public async Task Anticipada_rechaza_fecha_anterior_a_hoy_y_no_consulta_al_servicio()
    {
        using var ctx = new PedidosTestContext(Ahora);
        var servicio = new ServicioEspia();
        var ctrl = new AdminAvailabilityController(servicio, ctx.Fechas);

        var ex = await Assert.ThrowsAsync<AppException>(() => ctrl.ValidarAnticipada(new ValidateAdvanceRequest
        {
            ProductId = Guid.NewGuid(), Cantidad = 1, FechaEntrega = new DateOnly(2026, 10, 19)
        }));

        Assert.Equal("La fecha de entrega no puede ser anterior a hoy.", ex.Message);
        Assert.Equal(0, servicio.Llamadas);
    }

    [Fact]
    public async Task Anticipada_acepta_la_fecha_de_hoy_de_la_tienda()
    {
        using var ctx = new PedidosTestContext(Ahora);
        var servicio = new ServicioEspia();
        var ctrl = new AdminAvailabilityController(servicio, ctx.Fechas);

        await ctrl.ValidarAnticipada(new ValidateAdvanceRequest
        {
            ProductId = Guid.NewGuid(), Cantidad = 1, FechaEntrega = new DateOnly(2026, 10, 20)
        });

        Assert.Equal(1, servicio.Llamadas);
    }

    /// <summary>Servicio mínimo: solo cuenta las validaciones anticipadas por producto.</summary>
    private sealed class ServicioEspia : IAvailabilityService
    {
        public int Llamadas { get; private set; }

        public Task<AdvanceSaleValidationResult> ValidarVentaAnticipadaAsync(Guid productId, int cantidad, DateOnly fechaEntrega)
        {
            Llamadas++;
            return Task.FromResult(new AdvanceSaleValidationResult());
        }

        public Task<SupplyAvailabilityDto> ObtenerDisponibilidadInsumoAsync(Guid inventoryItemId) => throw new NotImplementedException();
        public Task<ProductAvailabilityDto> ObtenerDisponibilidadProductoAsync(Guid productId) => throw new NotImplementedException();
        public Task<SaleValidationResult> ValidarVentaInstantaneaAsync(Guid productId, int cantidad) => throw new NotImplementedException();
        public Task<AdvanceSaleValidationResult> ValidarVentaAnticipadaAsync(IReadOnlyList<(Guid ProductId, int Cantidad)> lineas, DateOnly fechaEntrega) => throw new NotImplementedException();
        public Task<SupplyCycleDto> ObtenerCicloAbastecimientoAsync() => throw new NotImplementedException();
        public Task RegistrarDemandaNoCubiertaAsync(Guid inventoryItemId, Guid? productId, int solicitada, int disponible, string origen) => throw new NotImplementedException();
    }
}
