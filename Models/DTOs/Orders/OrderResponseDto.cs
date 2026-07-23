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
    public decimal? CostoEnvio     { get; set; }
    public decimal  SaldoPendiente { get; set; }
    public string?  Notas          { get; set; }
    public string   NombreCliente  { get; set; } = string.Empty;
    public DateTime FechaCreacion  { get; set; }
    public bool     Archivado      { get; set; }

    public DireccionDto             Direccion { get; set; } = null!;
    public List<OrderItemResponseDto> Items   { get; set; } = [];
    public List<PaymentResponseDto>   Pagos   { get; set; } = [];
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
    public bool     Archivado      { get; set; }
    // Productos del pedido. Solo se llena en "Mis Pedidos" (null en listados admin).
    public List<OrderSummaryItemDto>? Items { get; set; }
}

// Vista mínima de un producto dentro del resumen de pedido (para "Mis Pedidos").
public class OrderSummaryItemDto
{
    public string  ProductName  { get; set; } = string.Empty;
    public string? ProductImage { get; set; }
    public int     Quantity     { get; set; }
    public decimal Price        { get; set; }
}

public class UpdateOrderStatusRequestDto
{
    public string NuevoEstado { get; set; } = string.Empty;
    public string? Notas      { get; set; }
}
