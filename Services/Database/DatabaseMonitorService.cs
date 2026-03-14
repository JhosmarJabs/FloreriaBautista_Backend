using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Database;
using FloreriaBautista.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Services.Database;

public class DatabaseMonitorService : IDatabaseMonitorService
{
    private readonly AppDbContext _context;
    public DatabaseMonitorService(AppDbContext context) => _context = context;

    public async Task<MonitorReportDto> GenerarReporteCompletoAsync() => new()
    {
        GeneradoEn    = DateTime.UtcNow,
        Tablas        = await ObtenerTamanioTablasAsync(),
        Conexiones    = await ObtenerConexionesActivasAsync(),
        IndicesSinUso = await ObtenerIndicesSinUsoAsync(),
        QueriesLentos = await ObtenerQueriesLentosAsync(),
        Estadisticas  = await ObtenerEstadisticasAsync()
    };

    // ── Tamaño de tablas ──────────────────────────────────────────
    public async Task<List<TablaInfoDto>> ObtenerTamanioTablasAsync()
    {
        var rows = await _context.Database
            .SqlQueryRaw<TablaRaw>(@"
                SELECT
                    c.relname                                      AS nombretabla,
                    s.n_live_tup                                   AS totalfilas,
                    pg_size_pretty(pg_table_size(c.oid))           AS tamanotabla,
                    pg_size_pretty(pg_indexes_size(c.oid))         AS tamanoindices,
                    pg_size_pretty(pg_total_relation_size(c.oid))  AS temanototal,
                    pg_total_relation_size(c.oid)                  AS temanototalbytes
                FROM pg_class c
                JOIN pg_stat_user_tables s ON s.relid = c.oid
                WHERE c.relkind = 'r'
                ORDER BY pg_total_relation_size(c.oid) DESC")
            .ToListAsync();

        return rows.Select(r => new TablaInfoDto
        {
            NombreTabla     = r.nombretabla,
            TotalFilas      = r.totalfilas,
            TamanoTabla     = r.tamanotabla,
            TamanoIndices   = r.tamanoindices,
            TamanoTotal     = r.temanototal,
            TamanoTotalBytes = r.temanototalbytes
        }).ToList();
    }

    // ── Conexiones activas ────────────────────────────────────────
    public async Task<List<ConexionActivaDto>> ObtenerConexionesActivasAsync()
    {
        var rows = await _context.Database
            .SqlQueryRaw<ConexionRaw>(@"
                SELECT
                    pid                                          AS pid,
                    usename                                      AS usuario,
                    datname                                      AS basedatos,
                    state                                        AS estado,
                    LEFT(query, 200)                             AS queryactual,
                    COALESCE(
                        ROUND(EXTRACT(EPOCH FROM (now() - query_start))::numeric, 3)::text || ' s',
                        '—'
                    )                                            AS duracionquery,
                    COALESCE(client_addr::text, 'local')         AS ipcliente
                FROM pg_stat_activity
                WHERE state IS NOT NULL
                ORDER BY query_start ASC NULLS LAST")
            .ToListAsync();

        return rows.Select(r => new ConexionActivaDto
        {
            Pid           = r.pid,
            Usuario       = r.usuario,
            BaseDatos     = r.basedatos,
            Estado        = r.estado,
            QueryActual   = r.queryactual,
            DuracionQuery = r.duracionquery,
            IpCliente     = r.ipcliente
        }).ToList();
    }

    // ── Índices sin uso ───────────────────────────────────────────
    public async Task<List<IndiceInfoDto>> ObtenerIndicesSinUsoAsync()
    {
        var rows = await _context.Database
            .SqlQueryRaw<IndiceRaw>(@"
                SELECT
                    i.relname                                    AS nombreindice,
                    t.relname                                    AS nombretabla,
                    array_to_string(array_agg(a.attname), ', ') AS columnas,
                    COALESCE(s.idx_scan, 0)                      AS vecesusado,
                    pg_size_pretty(pg_relation_size(i.oid))      AS tamano,
                    CASE
                        WHEN COALESCE(s.idx_scan, 0) = 0
                        THEN 'Nunca usado — considerar eliminar'
                        ELSE 'Uso bajo'
                    END                                          AS recomendacion
                FROM pg_index x
                JOIN pg_class i  ON i.oid = x.indexrelid
                JOIN pg_class t  ON t.oid = x.indrelid
                JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = ANY(x.indkey)
                LEFT JOIN pg_stat_user_indexes s ON s.indexrelid = x.indexrelid
                WHERE t.relkind = 'r'
                  AND NOT x.indisprimary
                  AND NOT x.indisunique
                  AND COALESCE(s.idx_scan, 0) < 10
                GROUP BY i.relname, t.relname, s.idx_scan, i.oid
                ORDER BY COALESCE(s.idx_scan, 0) ASC, pg_relation_size(i.oid) DESC")
            .ToListAsync();

        return rows.Select(r => new IndiceInfoDto
        {
            NombreIndice  = r.nombreindice,
            NombreTabla   = r.nombretabla,
            Columnas      = r.columnas,
            VecesUsado    = r.vecesusado,
            Tamano        = r.tamano,
            Recomendacion = r.recomendacion
        }).ToList();
    }

    // ── Queries lentos ────────────────────────────────────────────
    public async Task<List<QueryLentoDto>> ObtenerQueriesLentosAsync()
    {
        try
        {
            var rows = await _context.Database
                .SqlQueryRaw<QueryLentoRaw>(@"
                    SELECT
                        LEFT(query, 300)   AS query,
                        mean_exec_time     AS tiempopromedioMs,
                        calls              AS vecesejecutado,
                        total_exec_time    AS tiempototalms,
                        dbid::text         AS basedatos
                    FROM pg_stat_statements
                    WHERE mean_exec_time > 100
                    ORDER BY mean_exec_time DESC
                    LIMIT 20")
                .ToListAsync();

            return rows.Select(r => new QueryLentoDto
            {
                Query            = r.query,
                TiempoPromedioMs = r.tiempopromedioMs,
                VecesEjecutado   = r.vecesejecutado,
                TiempoTotalMs    = r.tiempototalms,
                BaseDatos        = r.basedatos
            }).ToList();
        }
        catch
        {
            return [new QueryLentoDto
            {
                Query = "Extensión pg_stat_statements no habilitada. " +
                        "Agrégala en postgresql.conf: shared_preload_libraries = 'pg_stat_statements'",
                BaseDatos = "N/A"
            }];
        }
    }

    // ── Estadísticas generales ────────────────────────────────────
    public async Task<EstadisticasBdDto> ObtenerEstadisticasAsync()
    {
        var rows = await _context.Database
            .SqlQueryRaw<EstadisticasRaw>(@"
                SELECT
                    pg_size_pretty(pg_database_size(current_database())) AS temanototalbd,
                    SUM(xact_commit + xact_rollback)::bigint             AS totaltransacciones,
                    SUM(blks_hit)::bigint                                AS cachehits,
                    SUM(blks_read)::bigint                               AS cachemisses,
                    CASE
                        WHEN SUM(blks_hit) + SUM(blks_read) = 0 THEN 0::numeric
                        ELSE ROUND(
                            (SUM(blks_hit)::numeric /
                            (SUM(blks_hit) + SUM(blks_read))) * 100, 2)
                    END                                                  AS porcentajecachehit,
                    COALESCE(
                        (SELECT MAX(last_vacuum)::text FROM pg_stat_user_tables),
                        'Sin datos'
                    )                                                    AS fechaultimovacuum
                FROM pg_stat_database
                WHERE datname = current_database()")
            .FirstOrDefaultAsync();

        if (rows == null) return new EstadisticasBdDto();

        return new EstadisticasBdDto
        {
            TamanoTotalBd       = rows.temanototalbd,
            TotalTransacciones  = rows.totaltransacciones,
            CacheHits           = rows.cachehits,
            CacheMisses         = rows.cachemisses,
            PorcentajeCacheHit  = (double)rows.porcentajecachehit,
            FechaUltimoVacuum   = rows.fechaultimovacuum
        };
    }

    // ── Clases internas para mapeo raw ────────────────────────────
    private class TablaRaw
    {
        public string nombretabla     { get; set; } = "";
        public long   totalfilas      { get; set; }
        public string tamanotabla     { get; set; } = "";
        public string tamanoindices   { get; set; } = "";
        public string temanototal     { get; set; } = "";
        public long   temanototalbytes { get; set; }
    }

    private class ConexionRaw
    {
        public int     pid           { get; set; }
        public string  usuario       { get; set; } = "";
        public string  basedatos     { get; set; } = "";
        public string  estado        { get; set; } = "";
        public string? queryactual   { get; set; }
        public string  duracionquery { get; set; } = "";
        public string  ipcliente     { get; set; } = "";
    }

    private class IndiceRaw
    {
        public string nombreindice  { get; set; } = "";
        public string nombretabla   { get; set; } = "";
        public string columnas      { get; set; } = "";
        public long   vecesusado    { get; set; }
        public string tamano        { get; set; } = "";
        public string recomendacion { get; set; } = "";
    }

    private class QueryLentoRaw
    {
        public string  query            { get; set; } = "";
        public double  tiempopromedioMs { get; set; }
        public long    vecesejecutado   { get; set; }
        public double  tiempototalms    { get; set; }
        public string  basedatos        { get; set; } = "";
    }

    private class EstadisticasRaw
    {
        public string  temanototalbd       { get; set; } = "";
        public long    totaltransacciones  { get; set; }
        public long    cachehits           { get; set; }
        public long    cachemisses         { get; set; }
        public decimal porcentajecachehit  { get; set; }
        public string  fechaultimovacuum   { get; set; } = "";
    }
}
