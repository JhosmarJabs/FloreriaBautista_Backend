namespace FloreriaBautista.Models.DTOs.Common;

public class PagedResultDto<T>
{
    public IEnumerable<T> Items       { get; set; } = [];
    public int            Total       { get; set; }
    public int            Pagina      { get; set; }
    public int            TamPagina   { get; set; }
    public int            TotalPaginas => (int)Math.Ceiling((double)Total / TamPagina);
}
