using System.Text.Json;
using FloreriaBautista.Models.DTOs.InstantSales;
using FloreriaBautista.Services.Realtime;
using Xunit;

namespace FloreriaBautista.Tests;

/// <summary>
/// FIX-006 (venta-instantanea-tiempo-real-05): SolicitudDecidida lleva al cliente solo seis campos.
/// </summary>
public class SolicitudDecididaPayloadTests
{
    [Fact]
    public void Payload_lleva_solo_los_seis_campos_en_camelCase()
    {
        var expira = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        var dto = new SolicitudVentaInstantaneaDto
        {
            Id = Guid.NewGuid(), CustomerId = Guid.NewGuid(), ProductId = Guid.NewGuid(),
            NombreCliente = "Ana", TelefonoCliente = "5551112222", NombreProducto = "Ramo",
            Cantidad = 2, Estado = "RECHAZADA", MotivoRechazo = "Sin stock",
            DecididaPorNombre = "Empleado Uno", ReservaExpiraEn = expira, OrderId = Guid.NewGuid()
        };

        var json = JsonSerializer.Serialize(
            SolicitudDecididaPayload.Desde(dto), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var doc = JsonDocument.Parse(json);
        var claves = doc.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(x => x).ToArray();

        Assert.Equal(
            new[] { "cantidad", "estado", "id", "motivoRechazo", "nombreProducto", "reservaExpiraEn" }
                .OrderBy(x => x).ToArray(),
            claves);
        Assert.Equal(dto.Id, doc.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("Sin stock", doc.RootElement.GetProperty("motivoRechazo").GetString());
        Assert.EndsWith("Z", doc.RootElement.GetProperty("reservaExpiraEn").GetString());
    }
}
