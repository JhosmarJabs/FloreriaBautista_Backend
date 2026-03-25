namespace FloreriaBautista.Services.Interfaces;

public interface IExportService
{
    Task<(byte[] Contenido, string NombreArchivo)> ExportarProductosAsync();
    Task<(byte[] Contenido, string NombreArchivo)> ExportarInventarioAsync();
    Task<(byte[] Contenido, string NombreArchivo)> ExportarFloresAsync();
}
