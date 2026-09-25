namespace FloreriaBautista.Models.DTOs.Common;

public class IndexResultDto<T>
{
    public List<T> Items           { get; set; } = [];
    public string  SincronizadoEn { get; set; } = string.Empty;
}
