using System.ComponentModel.DataAnnotations;

namespace FloreriaBautista.Models.DTOs.Orders;

public class CreateOrderRequestDto
{
    [Required] public DateOnly FechaEntrega { get; set; }
    public TimeOnly?           HoraEntrega  { get; set; }
    public string              TipoPedido   { get; set; } = "INSTANTANEO";
    public string?             Notas        { get; set; }

    [Required] public DireccionDto Direccion { get; set; } = null!;
    [Required] [MinLength(1)]
    public List<OrderItemRequestDto> Items   { get; set; } = [];
}

public class CreatePhysicalOrderRequestDto
{
    [Required] public string  NombreCliente { get; set; } = string.Empty;
    public string?            Telefono      { get; set; }
    [Required] public DateOnly FechaEntrega { get; set; }
    public TimeOnly?           HoraEntrega  { get; set; }
    public string              TipoPedido   { get; set; } = "INSTANTANEO";
    public string?             Notas        { get; set; }
    [Required] public DireccionDto Direccion { get; set; } = null!;
    [Required] [MinLength(1)]
    public List<OrderItemRequestDto> Items  { get; set; } = [];
}

public class DireccionDto
{
    [Required] public string Calle     { get; set; } = string.Empty;
    [Required] public string Colonia   { get; set; } = string.Empty;
    [Required] public string Municipio { get; set; } = string.Empty;
    [Required] public string Estado    { get; set; } = string.Empty;
    public string?           Cp        { get; set; }
    public string?           Referencias { get; set; }
}

public class OrderItemRequestDto
{
    [Required] public Guid ProductId  { get; set; }
    [Required] [Range(1, 999)]
    public int             Cantidad   { get; set; }
    public string?         Notas      { get; set; }
}
