using FloreriaBautista.Services.Interfaces;

namespace FloreriaBautista.Services.Employee;

/// <summary>
/// La ventana de datos que un empleado tiene permitido ver: él mismo, el día de
/// hoy, nada más. Todas las consultas del rol EMPLEADO se construyen a partir de
/// esta estructura en lugar de aceptar un rango de fechas del cliente, porque un
/// parámetro que viene del request es un parámetro que se puede manipular.
///
/// El día es el día natural de la tienda (America/Mexico_City vía
/// <see cref="IFechaHelper"/>), no el día UTC: a partir de las 18:00 locales el
/// UTC ya va en la fecha siguiente y el empleado perdería de vista su propia
/// jornada justo en las horas de más venta.
/// </summary>
public readonly record struct EmployeeScope
{
    /// <summary>Empleado dueño de los registros.</summary>
    public Guid     UsuarioId { get; init; }

    /// <summary>Día de calendario de la tienda al que está limitado.</summary>
    public DateOnly Dia       { get; init; }

    /// <summary>Instante UTC en que empieza el día local. Inclusivo.</summary>
    public DateTime InicioUtc { get; init; }

    /// <summary>Instante UTC en que empieza el día siguiente. Exclusivo — se
    /// compara con &lt; y no con &lt;=, para no capturar la medianoche exacta del
    /// día que sigue.</summary>
    public DateTime FinUtc    { get; init; }

    public static EmployeeScope DeHoy(Guid usuarioId, IFechaHelper fechas)
    {
        var hoy = fechas.HoyLocal();
        return new EmployeeScope
        {
            UsuarioId = usuarioId,
            Dia       = hoy,
            InicioUtc = fechas.InicioDelDiaUtc(hoy),
            FinUtc    = fechas.InicioDelDiaUtc(hoy.AddDays(1))
        };
    }

    /// <summary>¿Este instante UTC cae dentro del día del empleado?</summary>
    public bool Contiene(DateTime instanteUtc)
        => instanteUtc >= InicioUtc && instanteUtc < FinUtc;
}
