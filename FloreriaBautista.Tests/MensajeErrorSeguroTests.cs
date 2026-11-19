using FloreriaBautista.Services.Backups;
using Xunit;

namespace FloreriaBautista.Tests;

public class MensajeErrorSeguroTests
{
    // Valores ficticios: nunca se usa la llave real en las pruebas.
    private const string LlaveFalsa = "llave-falsa-123456";

    [Fact]
    public void Redacta_El_Valor_Y_Conserva_El_Motivo()
    {
        var r = MensajeErrorSeguro.Redactar($"Error al subir a Cloudinary: Invalid api_key {LlaveFalsa}", LlaveFalsa);

        Assert.Equal("Error al subir a Cloudinary: Invalid api_key [REDACTADO]", r);
        Assert.DoesNotContain(LlaveFalsa, r);
    }

    [Fact]
    public void Redacta_Sin_Distinguir_Mayusculas_Y_Todas_Las_Apariciones()
    {
        var r = MensajeErrorSeguro.Redactar($"{LlaveFalsa} y {LlaveFalsa.ToUpperInvariant()}", LlaveFalsa);

        Assert.Equal("[REDACTADO] y [REDACTADO]", r);
    }

    [Fact]
    public void Redacta_Varios_Secretos()
    {
        var r = MensajeErrorSeguro.Redactar("a secreto-uno-xx b secreto-dos-yy", "secreto-uno-xx", "secreto-dos-yy");

        Assert.Equal("a [REDACTADO] b [REDACTADO]", r);
    }

    [Fact]
    public void Ignora_Secretos_Nulos_Vacios_O_Muy_Cortos()
    {
        var texto = "File size too large. Got 12345678. Maximum is 10485760";

        Assert.Equal(texto, MensajeErrorSeguro.Redactar(texto, null, "", "12345"));
    }

    [Fact]
    public void Texto_Sin_Secretos_No_Cambia()
    {
        var texto = "Error al subir a Cloudinary: File size too large. Maximum is 10485760";

        Assert.Equal(texto, MensajeErrorSeguro.Redactar(texto, LlaveFalsa));
        Assert.Equal(texto, MensajeErrorSeguro.Redactar(texto));
    }

    [Fact]
    public void Mensaje_Nulo_O_Vacio_Se_Devuelve_Igual()
    {
        Assert.Null(MensajeErrorSeguro.Redactar(null, LlaveFalsa));
        Assert.Equal("", MensajeErrorSeguro.Redactar("", LlaveFalsa));
    }

    [Fact]
    public void Sin_Lista_De_Secretos_No_Falla()
    {
        Assert.Equal("texto", MensajeErrorSeguro.Redactar("texto", Array.Empty<string?>()));
    }
}
