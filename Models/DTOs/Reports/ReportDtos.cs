namespace FloreriaBautista.Models.DTOs.Reports;

public class SalesReportDto
{
    public DateOnly           Desde         { get; set; }
    public DateOnly           Hasta         { get; set; }
    public int                TotalPedidos  { get; set; }
    public decimal            TotalVentas   { get; set; }
    public decimal            PromedioVenta { get; set; }
    public List<DailySaleDto> VentasPorDia  { get; set; } = [];
}

public class DailySaleDto
{
    public DateOnly Fecha   { get; set; }
    public int      Pedidos { get; set; }
    public decimal  Total   { get; set; }
}

public class TopProductDto
{
    public Guid    ProductId { get; set; }
    public string  Nombre    { get; set; } = string.Empty;
    public int     Vendidos  { get; set; }
    public decimal Ingresos  { get; set; }
}

public class TopCustomerDto
{
    public Guid    CustomerId   { get; set; }
    public string  Nombre       { get; set; } = string.Empty;
    public int     Pedidos      { get; set; }
    public decimal TotalGastado { get; set; }
}

public class InventoryReportDto
{
    public int                TotalInsumos     { get; set; }
    public int                InsumosBajoStock { get; set; }
    public int                InsumosSinStock  { get; set; }
    public List<LowStockDto>  BajoStock        { get; set; } = [];
}

public class LowStockDto
{
    public Guid   ItemId      { get; set; }
    public string Nombre      { get; set; } = string.Empty;
    public int    StockActual { get; set; }
    public int    StockMinimo { get; set; }
}

public class DashboardStatsDto
{
    public decimal            TotalSales        { get; set; }
    public int                OrderCount        { get; set; }
    public decimal            AverageTicket     { get; set; }
    public int                NewCustomers      { get; set; }
    public List<DailySaleDto> WeeklySales       { get; set; } = [];
    public List<LowStockDto>  CriticalInventory { get; set; } = [];
}
