using System.ComponentModel.DataAnnotations;
using FloreriaBautista.Controllers.Admin;
using Xunit;

namespace FloreriaBautista.Tests;

/// <summary>
/// FIX-010 (inventario-disponibilidad-ventas-prediccion): el PUT de supply-settings es reemplazo total,
/// con los nueve campos obligatorios y acotados (ADM-CFG-003 y ADM-CFG-004).
/// </summary>
public class AdminSupplySettingsValidacionTests
{
    // Configuración por defecto (la que restaura el Tester).
    private static ActualizarSupplySettingsRequest Base() => new()
    {
        DiaAbastecimiento = 1, HoraCutoff = "12:00", DiaCutoff = 6, ToleranciaUnidades = 5,
        ToleranciaPorcentaje = 0.10m, UsarToleranciaAbsoluta = true, MargenSeguridadPorcentaje = 0.10m,
        DiasMinimosAnticipacion = 7, PermitirVentaSinReceta = true
    };

    private static IList<ValidationResult> Validar(object modelo)
    {
        var resultados = new List<ValidationResult>();
        Validator.TryValidateObject(modelo, new ValidationContext(modelo), resultados, validateAllProperties: true);
        return resultados;
    }

    private static void Invalido(ActualizarSupplySettingsRequest r, string campo)
    {
        var errores = Validar(r);
        Assert.NotEmpty(errores);
        Assert.Contains(errores, e => e.MemberNames.Contains(campo));
    }

    [Fact]
    public void Configuracion_por_defecto_es_valida() => Assert.Empty(Validar(Base()));

    [Fact]
    public void Cuerpo_completo_de_ADM_CFG_002_es_valido()
    {
        var r = Base();
        r.DiaAbastecimiento = 3; r.HoraCutoff = "09:30"; r.DiaCutoff = 2; r.ToleranciaUnidades = 8;
        r.ToleranciaPorcentaje = 0.25m; r.UsarToleranciaAbsoluta = false; r.MargenSeguridadPorcentaje = 0.15m;
        r.DiasMinimosAnticipacion = 3; r.PermitirVentaSinReceta = false;
        Assert.Empty(Validar(r));
    }

    [Fact] public void Variante_a_dia_abastecimiento_7() { var r = Base(); r.DiaAbastecimiento = 7; Invalido(r, nameof(r.DiaAbastecimiento)); }
    [Fact] public void Variante_b_dia_abastecimiento_negativo() { var r = Base(); r.DiaAbastecimiento = -1; Invalido(r, nameof(r.DiaAbastecimiento)); }
    [Fact] public void Variante_c_dia_cutoff_9() { var r = Base(); r.DiaCutoff = 9; Invalido(r, nameof(r.DiaCutoff)); }
    [Fact] public void Variante_d_hora_25_99() { var r = Base(); r.HoraCutoff = "25:99"; Invalido(r, nameof(r.HoraCutoff)); }
    [Fact] public void Variante_e_hora_abc() { var r = Base(); r.HoraCutoff = "abc"; Invalido(r, nameof(r.HoraCutoff)); }
    [Fact] public void Variante_f_tolerancia_unidades_negativa() { var r = Base(); r.ToleranciaUnidades = -5; Invalido(r, nameof(r.ToleranciaUnidades)); }
    [Fact] public void Variante_g_tolerancia_porcentaje_1_5() { var r = Base(); r.ToleranciaPorcentaje = 1.5m; Invalido(r, nameof(r.ToleranciaPorcentaje)); }
    [Fact] public void Variante_h_tolerancia_porcentaje_negativa() { var r = Base(); r.ToleranciaPorcentaje = -0.1m; Invalido(r, nameof(r.ToleranciaPorcentaje)); }
    [Fact] public void Variante_i_margen_negativo() { var r = Base(); r.MargenSeguridadPorcentaje = -0.5m; Invalido(r, nameof(r.MargenSeguridadPorcentaje)); }
    [Fact] public void Variante_j_dias_minimos_negativos() { var r = Base(); r.DiasMinimosAnticipacion = -1; Invalido(r, nameof(r.DiasMinimosAnticipacion)); }

    [Theory]
    [InlineData("")]
    [InlineData("7:00")]
    [InlineData("24:00")]
    [InlineData("12:60")]
    public void Hora_mal_formada_es_invalida(string hora)
    {
        var r = Base(); r.HoraCutoff = hora;
        Invalido(r, nameof(r.HoraCutoff));
    }

    [Fact]
    public void Cuerpo_vacio_deja_invalidos_los_nueve_campos()
    {
        var errores = Validar(new ActualizarSupplySettingsRequest());
        var campos = errores.SelectMany(e => e.MemberNames).Distinct().ToList();
        Assert.Equal(9, campos.Count);
    }

    [Fact]
    public void Cuerpo_parcial_B_de_ADM_CFG_004_es_invalido()
    {
        var r = new ActualizarSupplySettingsRequest { DiaAbastecimiento = 3, HoraCutoff = "09:30" };
        var campos = Validar(r).SelectMany(e => e.MemberNames).Distinct().ToList();
        Assert.Equal(7, campos.Count);
        Assert.DoesNotContain(nameof(r.DiaAbastecimiento), campos);
        Assert.DoesNotContain(nameof(r.HoraCutoff), campos);
    }

    [Fact]
    public void Limites_validos_se_aceptan()
    {
        var r = Base();
        r.DiaAbastecimiento = 0; r.DiaCutoff = 6; r.HoraCutoff = "00:00"; r.ToleranciaUnidades = 0;
        r.ToleranciaPorcentaje = 0m; r.MargenSeguridadPorcentaje = 1m; r.DiasMinimosAnticipacion = 0;
        Assert.Empty(Validar(r));
        r.HoraCutoff = "23:59"; r.DiaAbastecimiento = 6; r.DiaCutoff = 0; r.ToleranciaPorcentaje = 1m;
        Assert.Empty(Validar(r));
    }

    [Fact]
    public void Mensaje_de_error_identifica_el_campo()
    {
        var r = Base(); r.DiaAbastecimiento = 7;
        Assert.Contains(Validar(r), e => e.ErrorMessage!.Contains("diaAbastecimiento"));
    }
}
