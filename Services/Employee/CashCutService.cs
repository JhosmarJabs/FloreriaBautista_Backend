using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Employee;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Services.Employee;

public class CashCutService : ICashCutService
{
    private readonly AppDbContext  _context;
    private readonly IFechaHelper  _fechas;
    private readonly IAuditService _audit;

    public CashCutService(AppDbContext context, IFechaHelper fechas, IAuditService audit)
    {
        _context = context;
        _fechas  = fechas;
        _audit   = audit;
    }

    public async Task<CashCutPreviewDto> PreviewAsync(Guid usuarioId)
    {
        var scope     = EmployeeScope.DeHoy(usuarioId, _fechas);
        var totales   = await CalcularTotalesAsync(scope);
        var yaCerrado = await CorteDelDiaAsync(scope) is not null;

        return new CashCutPreviewDto
        {
            Dia              = scope.Dia,
            TotalVentas      = totales.TotalVentas,
            TotalEfectivo    = totales.TotalEfectivo,
            TotalGastos      = totales.TotalGastos,
            EfectivoEsperado = totales.EfectivoEsperado,
            PedidosContados  = totales.PedidosContados,
            GastosContados   = totales.Gastos.Count,
            YaCerrado        = yaCerrado
        };
    }

    public async Task<CashCutDto> CerrarAsync(Guid usuarioId, CreateCashCutRequestDto request)
    {
        var scope = EmployeeScope.DeHoy(usuarioId, _fechas);

        // El índice único en (usuario, día) es la garantía real contra el doble
        // cierre; esta comprobación solo existe para devolver un mensaje legible
        // en vez de un error de constraint de Postgres.
        if (await CorteDelDiaAsync(scope) is not null)
            throw new AppException("Ya cerraste tu corte de caja de hoy.");

        var totales = await CalcularTotalesAsync(scope);

        var corte = new CashCut
        {
            Id                = Guid.NewGuid(),
            UsuarioId         = usuarioId,
            FechaLocal        = scope.Dia,
            FechaHoraCierre   = DateTime.UtcNow,
            PeriodoInicioUtc  = scope.InicioUtc,
            PeriodoFinUtc     = scope.FinUtc,
            TotalVentas       = totales.TotalVentas,
            TotalEfectivo     = totales.TotalEfectivo,
            TotalGastos       = totales.TotalGastos,
            EfectivoEsperado  = totales.EfectivoEsperado,
            EfectivoDeclarado = request.EfectivoDeclarado,
            Diferencia        = request.EfectivoDeclarado - totales.EfectivoEsperado,
            PedidosContados   = totales.PedidosContados,
            GastosContados    = totales.Gastos.Count,
            Estado            = "CERRADO",
            Notas             = request.Notas?.Trim()
        };

        _context.CashCuts.Add(corte);

        // Sellar los gastos contra este corte: es lo que impide que un corte
        // posterior vuelva a restarlos si alguno quedó en la frontera del día.
        foreach (var gasto in totales.Gastos)
            gasto.CashCutId = corte.Id;

        await _context.SaveChangesAsync();

        await _audit.RegistrarAsync("CERRAR", "CashCut", corte.Id.ToString(), usuarioId,
            new { corte.FechaLocal, corte.EfectivoEsperado, corte.EfectivoDeclarado, corte.Diferencia });

        return MapToDto(corte, null);
    }

    public async Task<CashCutDto?> ObtenerDelDiaAsync(Guid usuarioId)
    {
        var scope = EmployeeScope.DeHoy(usuarioId, _fechas);
        var corte = await CorteDelDiaAsync(scope);
        if (corte is null) return null;

        var estados = await EstadosDeReporteAsync([corte.Id]);
        return MapToDto(corte, estados.GetValueOrDefault(corte.Id));
    }

    // ── Admin: sin restricción de dueño ni de fecha ───────────────
    public async Task<PagedResultDto<CashCutDto>> ListarAdminAsync(
        Guid? usuarioId, DateOnly? desde, DateOnly? hasta, int page, int size)
    {
        var query = _context.CashCuts.Include(c => c.Usuario).AsQueryable();

        if (usuarioId.HasValue) query = query.Where(c => c.UsuarioId  == usuarioId.Value);
        if (desde.HasValue)     query = query.Where(c => c.FechaLocal >= desde.Value);
        if (hasta.HasValue)     query = query.Where(c => c.FechaLocal <= hasta.Value);

        var total = await query.CountAsync();
        var suma  = total == 0 ? 0m : await query.SumAsync(c => c.TotalVentas);

        var items = await query
            .OrderByDescending(c => c.FechaLocal).ThenByDescending(c => c.FechaHoraCierre)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        var estados = await EstadosDeReporteAsync(items.Select(c => c.Id).ToList());

        return new PagedResultDto<CashCutDto>
        {
            Items = items.Select(c =>
            {
                var dto = MapToDto(c, estados.GetValueOrDefault(c.Id));
                dto.Empleado = c.Usuario?.Nombre;
                return dto;
            }).ToList(),
            Total        = total,
            Pagina       = page,
            TamanoPagina = size,
            TotalPaginas = (int)Math.Ceiling(total / (double)size),
            SumaTotal    = suma
        };
    }

    // ── Cálculo ───────────────────────────────────────────────────
    private sealed record TotalesDelDia(
        decimal TotalVentas, decimal TotalEfectivo, decimal TotalGastos,
        decimal EfectivoEsperado, int PedidosContados, List<Expense> Gastos);

    private async Task<TotalesDelDia> CalcularTotalesAsync(EmployeeScope scope)
    {
        // Ventas: pedidos que este empleado capturó hoy. Se cuenta por
        // FechaCreacion y no por FechaEntrega, porque el corte cuadra el trabajo
        // del turno, no las entregas programadas para otros días.
        var ventas = _context.Orders
            .Where(o => o.AtendidoPorUsuarioId == scope.UsuarioId
                     && o.FechaCreacion >= scope.InicioUtc
                     && o.FechaCreacion <  scope.FinUtc
                     && o.EstadoPedido  != "CANCELADO");

        var pedidosContados = await ventas.CountAsync();
        var totalVentas     = pedidosContados == 0 ? 0m : await ventas.SumAsync(o => o.Total);

        // Efectivo: los cobros reales, no el total facturado. Un pedido anticipado
        // puede haber dejado solo un anticipo, y lo que tiene que cuadrar contra la
        // caja es el dinero que entró, no lo que se vendió.
        var pagosEfectivo = _context.Payments
            .Where(p => p.Metodo    == "EFECTIVO"
                     && p.Estado    == "REGISTRADO"
                     && p.FechaPago >= scope.InicioUtc
                     && p.FechaPago <  scope.FinUtc
                     && p.Order.AtendidoPorUsuarioId == scope.UsuarioId);

        var totalEfectivo = await pagosEfectivo.AnyAsync()
            ? await pagosEfectivo.SumAsync(p => p.Monto)
            : 0m;

        // Gastos del día que ningún corte haya sellado todavía.
        var gastos = await _context.Expenses
            .Where(e => e.UsuarioId == scope.UsuarioId
                     && e.Estado    == "REGISTRADO"
                     && e.CashCutId == null
                     && e.FechaHora >= scope.InicioUtc
                     && e.FechaHora <  scope.FinUtc)
            .ToListAsync();

        var totalGastos = gastos.Sum(g => g.Importe);

        // Los gastos de personal se asumen pagados de la caja, por eso se restan
        // del efectivo esperado. Si algún día un gasto pudiera pagarse con tarjeta,
        // este cálculo tendría que distinguir el método de pago.
        return new TotalesDelDia(
            totalVentas, totalEfectivo, totalGastos,
            totalEfectivo - totalGastos, pedidosContados, gastos);
    }

    private Task<CashCut?> CorteDelDiaAsync(EmployeeScope scope)
        => _context.CashCuts.FirstOrDefaultAsync(c => c.UsuarioId  == scope.UsuarioId
                                                   && c.FechaLocal == scope.Dia
                                                   && c.Estado     != "ANULADO");

    private async Task<Dictionary<Guid, string>> EstadosDeReporteAsync(List<Guid> corteIds)
    {
        if (corteIds.Count == 0) return [];

        return await _context.ErrorReports
            .Where(r => r.TipoRegistro == "CORTE" && corteIds.Contains(r.RegistroId))
            .GroupBy(r => r.RegistroId)
            .Select(g => new
            {
                RegistroId = g.Key,
                Estado     = g.OrderByDescending(r => r.FechaHora).First().Estado
            })
            .ToDictionaryAsync(x => x.RegistroId, x => x.Estado);
    }

    private static CashCutDto MapToDto(CashCut c, string? estadoReporte) => new()
    {
        Id                = c.Id,
        FechaLocal        = c.FechaLocal,
        FechaHoraCierre   = c.FechaHoraCierre,
        TotalVentas       = c.TotalVentas,
        TotalEfectivo     = c.TotalEfectivo,
        TotalGastos       = c.TotalGastos,
        EfectivoEsperado  = c.EfectivoEsperado,
        EfectivoDeclarado = c.EfectivoDeclarado,
        Diferencia        = c.Diferencia,
        PedidosContados   = c.PedidosContados,
        GastosContados    = c.GastosContados,
        Estado            = c.Estado,
        Notas             = c.Notas,
        EstadoReporte     = estadoReporte
    };
}
