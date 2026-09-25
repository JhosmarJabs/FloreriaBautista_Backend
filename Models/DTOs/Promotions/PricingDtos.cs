namespace FloreriaBautista.Models.DTOs.Promotions;

public class AplicarCuponRequestDto
{
    public string CodigoCupon { get; set; } = string.Empty;
    public List<PricingItemDto> Items { get; set; } = [];
}

public class PricingItemDto
{
    public Guid ProductId { get; set; }
    public int  Cantidad  { get; set; }
}

public class PricingBreakdownDto
{
    public decimal Subtotal { get; set; }
    public List<DescuentoAplicadoDto> DescuentosAplicados { get; set; } = [];
    public decimal TotalDescuentos { get; set; }
    public decimal Total { get; set; }
}

public class DescuentoAplicadoDto
{
    public string  Origen { get; set; } = string.Empty; // OFERTA | DESCUENTO_AUTO | CUPON
    public string  Nombre { get; set; } = string.Empty;
    public decimal Monto  { get; set; }
}
