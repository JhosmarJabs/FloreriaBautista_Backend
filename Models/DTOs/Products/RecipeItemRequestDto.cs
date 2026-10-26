namespace FloreriaBautista.Models.DTOs.Products;

// Fila de receta que recibe POST /api/admin/recipes/{productId}.
// El campo se llama CantidadRequerida (no Cantidad) para conservar el contrato del endpoint.
public class RecipeItemRequestDto
{
    public Guid InventoryItemId   { get; set; }
    public int  CantidadRequerida { get; set; }
}
