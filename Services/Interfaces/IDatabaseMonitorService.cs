using FloreriaBautista.Models.DTOs.Database;

namespace FloreriaBautista.Services.Interfaces;

public interface IDatabaseMonitorService
{
    Task<MonitorReportDto>         GenerarReporteCompletoAsync();
    Task<List<TablaInfoDto>>       ObtenerTamanioTablasAsync();
    Task<List<ConexionActivaDto>>  ObtenerConexionesActivasAsync();
    Task<List<IndiceInfoDto>>      ObtenerIndicesSinUsoAsync();
    Task<List<QueryLentoDto>>      ObtenerQueriesLentosAsync();
    Task<EstadisticasBdDto>        ObtenerEstadisticasAsync();
}
