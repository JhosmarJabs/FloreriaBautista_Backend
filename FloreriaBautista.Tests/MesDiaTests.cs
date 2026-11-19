using FloreriaBautista.Helpers;
using Xunit;

namespace FloreriaBautista.Tests;

public class MesDiaTests
{
    [Theory]
    [InlineData("02-14")]
    [InlineData("02-29")]
    [InlineData("12-31")]
    [InlineData("01-01")]
    public void EsValido_Acepta_Ventanas_Reales(string valor) => Assert.True(MesDia.EsValido(valor));

    [Theory]
    [InlineData("13-45")]
    [InlineData("00-10")]
    [InlineData("02-30")]
    [InlineData("04-31")]
    [InlineData("2-14")]
    [InlineData("02/14")]
    [InlineData("ab-cd")]
    [InlineData("")]
    [InlineData(null)]
    public void EsValido_Rechaza_Ventanas_Imposibles(string? valor) => Assert.False(MesDia.EsValido(valor));

    [Theory]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    [InlineData(" 02-14 ", "02-14")]
    public void Normalizar_Vacio_Es_Null_Y_Recorta(string? entrada, string? esperado) =>
        Assert.Equal(esperado, MesDia.Normalizar(entrada));
}
