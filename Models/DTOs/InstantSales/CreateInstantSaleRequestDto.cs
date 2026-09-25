using System.ComponentModel.DataAnnotations;

namespace FloreriaBautista.Models.DTOs.InstantSales;

public class CreateInstantSaleRequestDto
{
    [Required]
    public Guid ProductId { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser al menos 1.")]
    public int Cantidad { get; set; }
}
