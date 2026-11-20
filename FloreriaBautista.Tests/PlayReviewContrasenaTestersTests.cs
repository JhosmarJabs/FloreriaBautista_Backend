using FloreriaBautista.Services.Auth;
using Xunit;

namespace FloreriaBautista.Tests;

// TEMPORAL-TESTERS: eliminar cuando Google Play apruebe la app para producción.
public class PlayReviewContrasenaTestersTests
{
    private static readonly string Hash = BCrypt.Net.BCrypt.HashPassword("prueba1234", 4);

    [Theory]
    [InlineData("prueba1234")]
    [InlineData("prueba1234 ")]
    [InlineData(" prueba1234\n")]
    [InlineData("Prueba1234")]
    [InlineData("PRUEBA1234 ")]
    public void En_revision_tolera_espacios_y_mayusculas(string escrita) =>
        Assert.True(AuthService.ContrasenaValida(escrita, Hash, modoRevision: true));

    [Theory]
    [InlineData("prueba1234 ")]
    [InlineData("Prueba1234")]
    public void Fuera_de_revision_la_comparacion_es_exacta(string escrita) =>
        Assert.False(AuthService.ContrasenaValida(escrita, Hash, modoRevision: false));

    [Fact]
    public void Una_contrasena_distinta_nunca_pasa() =>
        Assert.False(AuthService.ContrasenaValida("prueba123", Hash, modoRevision: true));
}
