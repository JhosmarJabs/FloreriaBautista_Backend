using FloreriaBautista.Services.Auth;
using Xunit;

namespace FloreriaBautista.Tests;

/// <summary>Pruebas unitarias de la política pura de renovación (sin BD).</summary>
public class PoliticaRenovacionTests
{
    private static readonly DateTime Ahora = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid U = Guid.NewGuid();

    private static TokenRenovacion Activo(string tipo = "REFRESH", Guid? user = null, DateTime? expira = null) =>
        new(tipo, user ?? U, expira ?? Ahora.AddDays(30), false, null, null);

    private static TokenRenovacion Rotado(int haceSegundos) =>
        new("REFRESH", U, Ahora.AddDays(30), true, PoliticaRenovacion.Rotado, Ahora.AddSeconds(-haceSegundos));

    private static DecisionRenovacion D(TokenRenovacion? t, TokenRenovacion? s = null,
        bool activo = true, int ventana = 60, bool deteccion = true) =>
        PoliticaRenovacion.Decidir(t, s, activo, Ahora, ventana, deteccion);

    [Fact] public void Inexistente_Rechaza() => Assert.Equal(DecisionRenovacion.Rechazar, D(null));

    [Fact] public void OtroTipo_Rechaza() =>
        Assert.Equal(DecisionRenovacion.Rechazar, D(Activo("PASSWORD_RESET")));

    [Fact] public void Vencido_Rechaza() =>
        Assert.Equal(DecisionRenovacion.Rechazar, D(Activo(expira: Ahora.AddSeconds(-1))));

    [Fact] public void VencidoExacto_Rechaza() =>
        Assert.Equal(DecisionRenovacion.Rechazar, D(Activo(expira: Ahora)));

    [Fact] public void CuentaInactiva_Rechaza() =>
        Assert.Equal(DecisionRenovacion.Rechazar, D(Activo(), activo: false));

    [Fact] public void CuentaInactiva_ConRotadoEnVentana_Rechaza() =>
        Assert.Equal(DecisionRenovacion.Rechazar, D(Rotado(5), Activo(), activo: false));

    [Fact] public void Activo_Rota() => Assert.Equal(DecisionRenovacion.Rotar, D(Activo()));

    [Theory]
    [InlineData("LOGOUT")]
    [InlineData("LOGOUT_TODAS")]
    [InlineData("RESET_PASSWORD")]
    [InlineData("CUENTA_INACTIVA")]
    [InlineData("REUSO_DETECTADO")]
    public void CerradoPorOtroMotivo_Rechaza_AunqueEsteEnVentana(string motivo)
    {
        var t = new TokenRenovacion("REFRESH", U, Ahora.AddDays(30), true, motivo, Ahora.AddSeconds(-5));
        Assert.Equal(DecisionRenovacion.Rechazar, D(t, Activo()));
    }

    [Fact] public void UsadoSinMotivo_Rechaza()
    {
        var t = new TokenRenovacion("REFRESH", U, Ahora.AddDays(30), true, null, null);
        Assert.Equal(DecisionRenovacion.Rechazar, D(t, Activo()));
    }

    [Fact] public void RotadoEnVentana_SucesorActivo_Reintenta() =>
        Assert.Equal(DecisionRenovacion.Reintentar, D(Rotado(2), Activo()));

    [Fact] public void BordeVentana_Igual_Reintenta() =>
        Assert.Equal(DecisionRenovacion.Reintentar, D(Rotado(60), Activo()));

    [Fact] public void BordeVentana_MasUnSegundo_ReusoDetectado() =>
        Assert.Equal(DecisionRenovacion.ReusoDetectado, D(Rotado(61), Activo()));

    [Fact] public void RotadoEnVentana_SinSucesor_Rechaza() =>
        Assert.Equal(DecisionRenovacion.Rechazar, D(Rotado(2), null));

    [Fact] public void RotadoEnVentana_SucesorUsado_Rechaza()
    {
        var s = new TokenRenovacion("REFRESH", U, Ahora.AddDays(30), true, "LOGOUT", Ahora);
        Assert.Equal(DecisionRenovacion.Rechazar, D(Rotado(2), s));
    }

    [Fact] public void RotadoEnVentana_SucesorVencido_Rechaza() =>
        Assert.Equal(DecisionRenovacion.Rechazar, D(Rotado(2), Activo(expira: Ahora.AddSeconds(-1))));

    [Fact] public void RotadoEnVentana_SucesorDeOtroUsuario_Rechaza() =>
        Assert.Equal(DecisionRenovacion.Rechazar, D(Rotado(2), Activo(user: Guid.NewGuid())));

    [Fact] public void RotadoEnVentana_SucesorDeOtroTipo_Rechaza() =>
        Assert.Equal(DecisionRenovacion.Rechazar, D(Rotado(2), Activo("PASSWORD_RESET")));

    [Fact] public void VentanaCero_RotadoInmediato_ReusoDetectado() =>
        Assert.Equal(DecisionRenovacion.ReusoDetectado, D(Rotado(0), Activo(), ventana: 0));

    [Fact] public void VentanaCero_SinDeteccion_Rechaza() =>
        Assert.Equal(DecisionRenovacion.Rechazar, D(Rotado(0), Activo(), ventana: 0, deteccion: false));

    [Fact] public void FueraDeVentana_SinDeteccion_Rechaza() =>
        Assert.Equal(DecisionRenovacion.Rechazar, D(Rotado(600), Activo(), deteccion: false));

    [Fact] public void RotadoSinCerradoEn_SeTrataFueraDeVentana() =>
        Assert.Equal(DecisionRenovacion.ReusoDetectado,
            D(new TokenRenovacion("REFRESH", U, Ahora.AddDays(30), true, "ROTADO", null), Activo()));

    [Fact] public void Reintento_NoDependeDeLaDeteccion() =>
        Assert.Equal(DecisionRenovacion.Reintentar, D(Rotado(2), Activo(), deteccion: false));
}
