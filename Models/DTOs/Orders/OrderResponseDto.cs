namespace FloreriaBautista.Models.DTOs.Orders;

public class OrderResponseDto
{
    public Guid     Id             { get; set; }
    public string   EstadoPedido   { get; set; } = string.Empty;
    public string   TipoPedido     { get; set; } = string.Empty;
    public string   Canal          { get; set; } = string.Empty;
    public DateOnly FechaEntrega   { get; set; }
    public TimeOnly? HoraEntrega   { get; set; }
    public decimal  Total          { get; set; }
    public decimal  SaldoPendiente { get; set; }
    public string?  Notas          { get; set; }
    public string   NombreCliente  { get; set; } = string.Empty;
    public DateTime FechaCreacion  { get; set; }

    public DireccionDto             Direccion { get; set; } = null!;
    public List<OrderItemResponseDto> Items   { get; set; } = [];
}

public class OrderItemResponseDto
{
    public Guid    Id             { get; set; }
    public Guid    ProductId      { get; set; }
    public string  NombreProducto { get; set; } = string.Empty;
    public int     Cantidad       { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal       { get; set; }
}

public class OrderSummaryDto
{
    public Guid     Id             { get; set; }
    public string   EstadoPedido   { get; set; } = string.Empty;
    public DateOnly FechaEntrega   { get; set; }
    public decimal  Total          { get; set; }
    public string   NombreCliente  { get; set; } = string.Empty;
    public DateTime FechaCreacion  { get; set; }
}

public class UpdateOrderStatusRequestDto
{
    public string NuevoEstado { get; set; } = string.Empty;
    public string? Notas      { get; set; }
}
