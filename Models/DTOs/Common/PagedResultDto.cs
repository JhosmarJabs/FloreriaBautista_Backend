namespace FloreriaBautista.Models.DTOs.Common;

public class PagedResultDto<T>
{
    public List<T> Items        { get; set; } = [];
    public int     Total        { get; set; }
    public int     Pagina       { get; set; } = 1;
    public int     TamanoPagina { get; set; } = 20;
    public int     TotalPaginas { get; set; }

    // Suma de los importes de TODOS los registros que cumplen el filtro (no solo
    // la página actual). Para listados de pedidos es la recaudación bruta real;
    // en listados sin importe queda en 0.
    public decimal SumaTotal    { get; set; }
}
