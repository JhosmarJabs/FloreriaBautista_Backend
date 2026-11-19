using FloreriaBautista.Models.DTOs.Employee;
using FloreriaBautista.Models.DTOs.Inventory;
using FloreriaBautista.Models.DTOs.Orders;
using FloreriaBautista.Models.DTOs.Products;
using FloreriaBautista.Models.DTOs.QuickSale;

namespace FloreriaBautista.Models.DTOs.Sync;

/// <summary>
/// Respuesta del endpoint consolidado de sync para el empleado.
/// En full sync (sin <c>desde</c>) trae todo lo del día + catálogo completo.
/// En delta sync trae solo lo modificado desde la marca.
/// </summary>
public class EmployeeSyncResponseDto
{
    public string  SincronizadoEn { get; set; } = string.Empty;
    public bool    EsDelta        { get; set; }

    public List<ProductIndexDto>        Productos  { get; set; } = [];
    public List<QuickSaleTemplateDto>   Plantillas { get; set; } = [];
    public List<OrderSummaryDto>        Ventas     { get; set; } = [];
    public List<ExpenseSyncDto>         Gastos     { get; set; } = [];
    public List<ErrorReportSyncDto>     Reportes   { get; set; } = [];
}

/// <summary>
/// Respuesta del endpoint consolidado de sync para el admin en el móvil.
/// </summary>
public class AdminMobileSyncResponseDto
{
    public string  SincronizadoEn { get; set; } = string.Empty;
    public bool    EsDelta        { get; set; }

    public List<ProductIndexDto>        Productos  { get; set; } = [];
    public List<InventoryIndexDto>      Inventario { get; set; } = [];
    public List<OrderSummaryDto>        Pedidos    { get; set; } = [];
    public List<QuickSaleTemplateDto>   Plantillas { get; set; } = [];
}

/// <summary>Vista de gasto para sync incremental (incluye ActualizadoEn).</summary>
public class ExpenseSyncDto
{
    public Guid     Id              { get; set; }
    public DateTime FechaHora       { get; set; }
    public string   Concepto        { get; set; } = string.Empty;
    public string   Categoria       { get; set; } = string.Empty;
    public decimal  Importe         { get; set; }
    public string   Estado          { get; set; } = string.Empty;
    public bool     IncluidoEnCorte { get; set; }
    public string?  EstadoReporte   { get; set; }
    public DateTime ActualizadoEn   { get; set; }
}

/// <summary>Vista de reporte de error para sync incremental (incluye ActualizadoEn).</summary>
public class ErrorReportSyncDto
{
    public Guid      Id               { get; set; }
    public string    TipoRegistro     { get; set; } = string.Empty;
    public Guid      RegistroId       { get; set; }
    public string    Motivo           { get; set; } = string.Empty;
    public string    Estado           { get; set; } = string.Empty;
    public DateTime  FechaHora        { get; set; }
    public DateTime? FechaResolucion  { get; set; }
    public string?   ResolucionAccion { get; set; }
    public string?   ResolucionNota   { get; set; }
    public DateTime  ActualizadoEn    { get; set; }
}
