namespace FloreriaBautista.Models.Entities;

/// <summary>
/// Corte de caja de un empleado: la foto de lo que recaudó y gastó en el día,
/// congelada en el momento de cerrar.
///
/// Los totales se guardan calculados en vez de recomputarse al consultarlos. Es a
/// propósito: si el admin corrige una venta después del corte, el corte debe
/// seguir diciendo lo que el empleado declaró al cerrar, que es contra lo que se
/// le cuadra el efectivo. La corrección se ve en la bitácora, no reescribiendo
/// el pasado.
/// </summary>
public class CashCut
{
    public Guid     Id                { get; set; }
    public Guid     UsuarioId         { get; set; }

    /// <summary>Día de calendario de la tienda que se cierra (hora local, no UTC).</summary>
    public DateOnly FechaLocal        { get; set; }

    public DateTime FechaHoraCierre   { get; set; } = DateTime.UtcNow;

    /// <summary>Ventana UTC exacta que abarcó el corte. Se guarda para poder
    /// auditar después qué entró y qué no, sin depender de que la zona horaria
    /// configurada siga siendo la misma.</summary>
    public DateTime PeriodoInicioUtc  { get; set; }
    public DateTime PeriodoFinUtc     { get; set; }

    public decimal  TotalVentas       { get; set; }
    public decimal  TotalEfectivo     { get; set; }
    public decimal  TotalGastos       { get; set; }

    /// <summary>Efectivo cobrado menos gastos pagados en efectivo.</summary>
    public decimal  EfectivoEsperado  { get; set; }

    /// <summary>Lo que el empleado contó físicamente en la caja.</summary>
    public decimal  EfectivoDeclarado { get; set; }

    /// <summary>Declarado − esperado. Negativo = falta dinero.</summary>
    public decimal  Diferencia        { get; set; }

    public int      PedidosContados   { get; set; }
    public int      GastosContados    { get; set; }

    /// <summary>CERRADO / ANULADO. El empleado no puede reabrirlo.</summary>
    public string   Estado            { get; set; } = "CERRADO";

    public string?  Notas             { get; set; }

    public User Usuario { get; set; } = null!;
    public ICollection<Expense> Gastos { get; set; } = [];
}
