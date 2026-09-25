namespace FloreriaBautista.Models.Entities;

/// <summary>
/// Gasto de personal: dinero que un empleado desembolsa por cuenta de la empresa
/// durante su jornada (taxi de una entrega, material de última hora, etc.).
///
/// Vive en una tabla general compartida por todos los empleados, igual que los
/// pedidos, pero cada renglón queda atado a su autor por <see cref="UsuarioId"/>.
/// Que estén juntos en la misma tabla no significa que se vean entre sí: el
/// aislamiento lo aplica el backend al construir la consulta, nunca la interfaz.
/// </summary>
public class Expense
{
    public Guid     Id        { get; set; }

    /// <summary>Empleado que realizó el gasto. Nunca nulo: sin autor no hay gasto.</summary>
    public Guid     UsuarioId { get; set; }

    public DateTime FechaHora { get; set; } = DateTime.UtcNow;
    public string   Concepto  { get; set; } = string.Empty;

    /// <summary>GASTO_PERSONAL por ahora; queda abierto para que el admin clasifique.</summary>
    public string   Categoria { get; set; } = "GASTO_PERSONAL";

    public decimal  Importe   { get; set; }

    /// <summary>REGISTRADO / ANULADO / CORREGIDO. El empleado solo puede crear en
    /// REGISTRADO; los otros dos estados son resultado de una acción del admin
    /// sobre un reporte de error.</summary>
    public string   Estado    { get; set; } = "REGISTRADO";

    /// <summary>Corte de caja que ya contabilizó este gasto. Null mientras esté
    /// abierto: es lo que impide contarlo dos veces en dos cortes distintos.</summary>
    public Guid?    CashCutId { get; set; }

    public string?  Notas     { get; set; }

    public User     Usuario   { get; set; } = null!;
    public CashCut? CashCut   { get; set; }
}
