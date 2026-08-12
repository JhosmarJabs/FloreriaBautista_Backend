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

    // Solo para el listado de pedidos: true cuando se pidió un rango de fechas que
    // incluye días anteriores a hoy sobre la vista ACTIVA, la cual por definición
    // solo contiene entregas de hoy en adelante. El backend no reinterpreta la
    // consulta; devuelve la bandera para que el frontend sugiera "ver en el
    // archivo" en vez de mostrar un resultado vacío sin explicación.
    public bool RangoFueraDeVistaActiva { get; set; }
}
