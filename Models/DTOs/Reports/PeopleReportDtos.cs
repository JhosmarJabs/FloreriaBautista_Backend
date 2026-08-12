namespace FloreriaBautista.Models.DTOs.Reports;

// ─── Desempeño de empleado ───────────────────────────────────────────────────
// Fuente: orders.AtendidoPorUsuarioId + audit_logs (transiciones de estado)
//         + deliveries (para repartidores)

public class StaffPerformanceReportDto
{
    /// Porcentaje de los pedidos del periodo que traen empleado asignado, 0-100.
    /// Los pedidos anteriores a la migración que agregó AtendidoPorUsuarioId no
    /// lo traen: sin este dato el reporte se leería como si nadie hubiera vendido.
    public decimal CoberturaAtribucion { get; set; }
    public int     PedidosSinAtribuir  { get; set; }
    public int     TotalPedidos        { get; set; }

    /// La medición del tiempo de preparación depende de que las transiciones de
    /// estado queden en audit_logs. Se registran desde que se instrumentó
    /// OrderService.CambiarEstadoAsync; los pedidos previos no aportan a la media.
    public int     PedidosConTiempoMedido { get; set; }

    public List<StaffMemberDto> Empleados { get; set; } = [];
}

public class StaffMemberDto
{
    public Guid     UsuarioId            { get; set; }
    public string   Nombre               { get; set; } = string.Empty;
    public int      PedidosAtendidos     { get; set; }
    public decimal  MontoVendido         { get; set; }
    public decimal  TicketPromedio       { get; set; }
    public int      PedidosNoCompletados { get; set; }
    public decimal  PctNoCompletados     { get; set; }
    /// Minutos promedio entre PENDIENTE_VALIDACION/EN_PREPARACION y EN_RUTA/ENTREGADO.
    /// Null si ninguno de sus pedidos tiene las transiciones registradas.
    public double?  MinutosPreparacion   { get; set; }

    // Métricas de repartidor (solo si tiene entregas asignadas)
    public int      EntregasAsignadas    { get; set; }
    public int      EntregasATiempo      { get; set; }
    public decimal? PctEntregasATiempo   { get; set; }
}

// ─── Cumplimiento de entregas ────────────────────────────────────────────────
// Fuente: deliveries.FechaProgramada/HoraProgramada vs FechaReal + orders.EstadoPedido

public class DeliveryFulfillmentReportDto
{
    public int      TotalEntregas       { get; set; }
    public int      Entregadas          { get; set; }
    public int      ATiempo             { get; set; }
    public int      ConRetraso          { get; set; }
    public int      SinFechaReal        { get; set; }
    public decimal? PctATiempo          { get; set; }
    /// Promedio de minutos de retraso, contando solo las que llegaron tarde.
    public double?  RetrasoPromedioMin  { get; set; }
    public int      PedidosNoCompletados{ get; set; }
    public decimal? PctNoCompletados    { get; set; }

    public List<TimeBucketDto>  Serie         { get; set; } = [];
    public List<BreakdownDto>   PorEstado     { get; set; } = [];
    public List<DeliveryLateDto> MasRetrasadas { get; set; } = [];
}

public class DeliveryLateDto
{
    public Guid      OrderId         { get; set; }
    public string    Cliente         { get; set; } = string.Empty;
    public DateOnly  FechaProgramada { get; set; }
    public TimeOnly? HoraProgramada  { get; set; }
    public DateTime? FechaReal       { get; set; }
    public double    RetrasoMinutos  { get; set; }
    public string?   Repartidor      { get; set; }
}

// ─── Clientes: nuevos vs recurrentes y recompra ──────────────────────────────
// Fuente: orders + customers + customer_segments

public class CustomerRetentionReportDto
{
    public int      ClientesActivos     { get; set; }
    public int      ClientesNuevos      { get; set; }
    public int      ClientesRecurrentes { get; set; }
    /// % de los clientes activos del periodo que ya habían comprado antes.
    public decimal? TasaRecompra        { get; set; }
    public decimal  VentasNuevos        { get; set; }
    public decimal  VentasRecurrentes   { get; set; }
    public decimal? TicketNuevos        { get; set; }
    public decimal? TicketRecurrentes   { get; set; }

    public List<TimeBucketDto> SerieNuevos      { get; set; } = [];
    public List<TimeBucketDto> SerieRecurrentes { get; set; } = [];
    /// Segmentación RFM ya calculada por CustomerSegmentationService. Vacío si
    /// el modelo todavía no se ha ejecutado.
    public List<BreakdownDto>  PorSegmento      { get; set; } = [];
    public List<TopCustomerDto> TopClientes     { get; set; } = [];
}

// ─── Cuentas por cobrar ──────────────────────────────────────────────────────
// Fuente: orders.SaldoPendiente + payments

public class ReceivablesReportDto
{
    public decimal TotalPorCobrar   { get; set; }
    public int     PedidosConSaldo  { get; set; }
    public decimal CobradoEnPeriodo { get; set; }

    /// Antigüedad medida desde Order.FechaCreacion hasta hoy.
    public List<BreakdownDto>     PorAntiguedad { get; set; } = [];
    public List<ReceivableDto>    Pedidos       { get; set; } = [];
}

public class ReceivableDto
{
    public Guid     OrderId        { get; set; }
    public string   Cliente        { get; set; } = string.Empty;
    public string?  Telefono       { get; set; }
    public DateTime FechaCreacion  { get; set; }
    public DateOnly FechaEntrega   { get; set; }
    public string   EstadoPedido   { get; set; } = string.Empty;
    public decimal  Total          { get; set; }
    public decimal  Pagado         { get; set; }
    public decimal  SaldoPendiente { get; set; }
    public int      DiasAntiguedad { get; set; }
}
