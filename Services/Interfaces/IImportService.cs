using FloreriaBautista.Models.DTOs.ImportExport;

namespace FloreriaBautista.Services.Interfaces;

public interface IImportService
{
    Task<ImportResultDto> ImportarProductosAsync(Stream csv, string nombreArchivo);
    Task<ImportResultDto> ImportarInventarioAsync(Stream csv, string nombreArchivo);
    Task<ImportResultDto> ImportarFloresAsync(Stream csv, string nombreArchivo);
}
