using System.ComponentModel.DataAnnotations;
using FloreriaBautista.Models.DTOs.Employee;
using Xunit;

namespace FloreriaBautista.Tests;

public class ExpenseImporteRangoTests
{
    private static bool Valido(decimal importe)
    {
        var dto = new CreateExpenseRequestDto { Concepto = "x", Importe = importe };
        return Validator.TryValidateObject(dto, new ValidationContext(dto), new List<ValidationResult>(), true);
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(99999999.99)]
    public void Importe_dentro_de_rango_es_valido(decimal importe) => Assert.True(Valido(importe));

    [Theory]
    [InlineData(0)]
    [InlineData(100000000)]
    public void Importe_fuera_de_rango_es_invalido(decimal importe) => Assert.False(Valido(importe));
}
