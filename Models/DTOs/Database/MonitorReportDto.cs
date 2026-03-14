namespace FloreriaBautista.Models.DTOs.Database;

/// <summary>Reporte completo de rendimiento del SGBD.</summary>
public class MonitorReportDto
{
    public DateTime GeneradoEn       { get; set; } = DateTime.UtcNow;
    public List<TablaInfoDto>        Tablas          { get; set; } = [];
    public List<ConexionActivaDto>   Conexiones      { get; set; } = [];
    public List<IndiceInfoDto>       IndicesSinUso   { get; set; } = [];
    public List<QueryLentoDto>       QueriesLentos   { get; set; } = [];
    public EstadisticasBdDto         Estadisticas    { get; set; } = new();
}

public class TablaInfoDto
{
    public string  NombreTabla     { get; set; } = string.Empty;
    public long    TotalFilas      { get; set; }
    public string  TamanoTabla     { get; set; } = string.Empty; // "4 MB"
    public string  TamanoIndices   { get; set; } = string.Empty;
    public string  TamanoTotal     { get; set; } = string.Empty;
    public long    TamanoTotalBytes { get; set; }
}

public class ConexionActivaDto
{
    public int     Pid             { get; set; }
    public string  Usuario         { get; set; } = string.Empty;
    public string  BaseDatos       { get; set; } = string.Empty;
    public string  Estado          { get; set; } = string.Empty; // active | idle | idle in transaction
    public string? QueryActual     { get; set; }
    public string  DuracionQuery   { get; set; } = string.Empty;
    public string  IpCliente       { get; set; } = string.Empty;
}

public class IndiceInfoDto
{
    public string  NombreIndice    { get; set; } = string.Empty;
    public string  NombreTabla     { get; set; } = string.Empty;
    public string  Columnas        { get; set; } = string.Empty;
    public long    VecesUsado      { get; set; }
    public string  Tamano          { get; set; } = string.Empty;
    public string  Recomendacion   { get; set; } = string.Empty; // "Considerar eliminar"
}

public class QueryLentoDto
{
    public string  Query           { get; set; } = string.Empty;
    public double  TiempoPromedioMs { get; set; }
    public long    VecesEjecutado  { get; set; }
    public double  TiempoTotalMs   { get; set; }
    public string  BaseDatos       { get; set; } = string.Empty;
}

public class EstadisticasBdDto
{
    public string  TamanoTotalBd   { get; set; } = string.Empty;
    public long    TotalTransacciones { get; set; }
    public long    CacheHits        { get; set; }
    public long    CacheMisses      { get; set; }
    public double  PorcentajeCacheHit { get; set; } // ideal > 99%
    public string  FechaUltimoVacuum { get; set; } = string.Empty;
}
