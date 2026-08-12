using FloreriaBautista.Services;
using FloreriaBautista.Tests.TestHelpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FloreriaBautista.Tests;

public class FechaHelperTests
{
    // 11 de agosto de 2026, 23:00 en la florería (UTC-6) = 12 de agosto 05:00 UTC.
    private static readonly DateTime Utc23HoraLocal = new(2026, 8, 12, 5, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void HoyLocal_A_Las_23_Devuelve_El_Dia_Que_Ve_La_Tienda()
    {
        var fechas = new FechaHelper(PedidosTestContext.ZonaTienda, () => Utc23HoraLocal);

        Assert.Equal(new DateOnly(2026, 8, 11), fechas.HoyLocal());
    }

    [Fact]
    public void HoyLocal_No_Coincide_Con_El_Dia_Utc_Al_Final_De_La_Tarde()
    {
        // Este es exactamente el bug que motivó el helper: usar el día UTC adelanta
        // la fecha seis horas antes de que termine el día del negocio.
        var fechas = new FechaHelper(PedidosTestContext.ZonaTienda, () => Utc23HoraLocal);

        Assert.Equal(new DateOnly(2026, 8, 12), DateOnly.FromDateTime(Utc23HoraLocal));
        Assert.Equal(new DateOnly(2026, 8, 11), fechas.HoyLocal());
    }

    [Fact]
    public void HoyLocal_A_Medio_Dia_Coincide_Con_El_Dia_Utc()
    {
        var mediodiaUtc = new DateTime(2026, 8, 11, 18, 0, 0, DateTimeKind.Utc); // 12:00 local
        var fechas = new FechaHelper(PedidosTestContext.ZonaTienda, () => mediodiaUtc);

        Assert.Equal(new DateOnly(2026, 8, 11), fechas.HoyLocal());
    }

    [Fact]
    public void InicioDelDiaUtc_Es_La_Medianoche_Local_Expresada_En_Utc()
    {
        var fechas = new FechaHelper(PedidosTestContext.ZonaTienda, () => Utc23HoraLocal);

        Assert.Equal(new DateTime(2026, 8, 11, 6, 0, 0, DateTimeKind.Utc),
                     fechas.InicioDelDiaUtc(new DateOnly(2026, 8, 11)));
    }

    [Fact]
    public void Zona_Configurada_Opera_En_Utc_Menos_6()
    {
        var fechas = CrearDesdeConfig("America/Mexico_City");

        Assert.Equal(TimeSpan.FromHours(-6),
                     fechas.Zona.GetUtcOffset(new DateTime(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Zona_Desconocida_Cae_Al_Respaldo_En_Vez_De_Reventar()
    {
        // El id de zona cambia entre Windows e IANA; si el sistema no reconoce el
        // configurado, el helper debe seguir dando la hora correcta de la tienda.
        var fechas = CrearDesdeConfig("Zona/Que_No_Existe");

        Assert.Equal(TimeSpan.FromHours(-6),
                     fechas.Zona.GetUtcOffset(new DateTime(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc)));
    }

    private static FechaHelper CrearDesdeConfig(string zonaId)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Store:TimeZone"] = zonaId })
            .Build();

        return new FechaHelper(config, NullLogger<FechaHelper>.Instance);
    }
}
