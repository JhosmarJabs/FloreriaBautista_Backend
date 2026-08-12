namespace FloreriaBautista.Models.DTOs.Reports;

// ─── Reporte de visitas ──────────────────────────────────────────────────────
// Fuente: page_visits (detalle, 90 días) + page_visits_daily (agregado permanente)

public class VisitsReportDto
{
    public int  TotalVisitas  { get; set; }
    public int  TotalSesiones { get; set; }
    /// Visitas del rango que ya solo existen en el agregado diario porque el
    /// detalle se purgó. Los cortes por producto/búsqueda no las incluyen.
    public bool IncluyeAgregadoHistorico { get; set; }

    public List<TimeBucketDto>   Serie        { get; set; } = [];
    public List<VisitRouteDto>   TopRutas     { get; set; } = [];
    public List<VisitProductDto> TopProductos { get; set; } = [];
    public List<BreakdownDto>    PorDispositivo { get; set; } = [];
    public List<BreakdownDto>    PorReferrer  { get; set; } = [];

    public VisitConversionDto     Conversion         { get; set; } = new();
    public List<FailedSearchDto>  BusquedasSinResultado { get; set; } = [];
}

public class VisitRouteDto
{
    public string Ruta     { get; set; } = string.Empty;
    public int    Visitas  { get; set; }
    public int    Sesiones { get; set; }
}

public class VisitProductDto
{
    public Guid     ProductId       { get; set; }
    public string   Nombre          { get; set; } = string.Empty;
    public int      Vistas          { get; set; }
    public int      Sesiones        { get; set; }
    public int      UnidadesVendidas{ get; set; }
    /// Unidades vendidas / vistas, 0-100. Null si no hubo vistas.
    public decimal? ConversionPct   { get; set; }
}

/// <summary>
/// Conversión visita → pedido, medida a nivel agregado: sesiones únicas del
/// periodo contra pedidos WEB creados en el mismo periodo.
///
/// No es atribución por sesión — para eso habría que guardar el SesionId en el
/// pedido, y hoy Order no lo tiene. Se declara explícito para que nadie lea el
/// número como si cada pedido estuviera ligado a una visita concreta.
/// </summary>
public class VisitConversionDto
{
    public int      SesionesUnicas { get; set; }
    public int      PedidosWeb     { get; set; }
    public decimal? TasaPct        { get; set; }
    public string   Metodo         { get; set; } =
        "Agregada: pedidos con canal WEB del periodo ÷ sesiones únicas del periodo.";
}

public class FailedSearchDto
{
    public string Termino   { get; set; } = string.Empty;
    public int    Busquedas { get; set; }
}
