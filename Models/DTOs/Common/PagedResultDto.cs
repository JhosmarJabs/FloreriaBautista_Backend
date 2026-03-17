namespace FloreriaBautista.Models.DTOs.Common;

public class PagedResultDto<T>
{
    public List<T> Items        { get; set; } = [];
    public int     Total        { get; set; }
    public int     Pagina       { get; set; } = 1;
    public int     TamanoPagina { get; set; } = 20;
    public int     TotalPaginas { get; set; }
}
