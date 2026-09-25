using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Employee;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Services.Employee;

public class EmployeeExpenseService : IEmployeeExpenseService
{
    private readonly AppDbContext  _context;
    private readonly IFechaHelper  _fechas;
    private readonly IAuditService _audit;

    public EmployeeExpenseService(AppDbContext context, IFechaHelper fechas, IAuditService audit)
    {
        _context = context;
        _fechas  = fechas;
        _audit   = audit;
    }

    public async Task<ExpenseDto> RegistrarAsync(Guid usuarioId, CreateExpenseRequestDto request)
    {
        // Un gasto que cae después del corte quedaría fuera de la caja que el
        // empleado ya cuadró y firmó. Se rechaza en vez de aceptarlo en silencio:
        // el descuadre saldría al día siguiente sin que nadie sepa de dónde vino.
        var scope = EmployeeScope.DeHoy(usuarioId, _fechas);
        if (await HayCorteCerradoAsync(scope))
            throw new AppException(
                "Ya cerraste tu corte de caja de hoy; este gasto ya no puede entrar. " +
                "Repórtalo al administrador.");

        var gasto = new Expense
        {
            Id        = Guid.NewGuid(),
            UsuarioId = usuarioId,
            FechaHora = DateTime.UtcNow,
            Concepto  = request.Concepto.Trim(),
            Categoria = "GASTO_PERSONAL",
            Importe   = request.Importe,
            Estado    = "REGISTRADO",
            Notas     = request.Notas?.Trim()
        };

        _context.Expenses.Add(gasto);
        await _context.SaveChangesAsync();

        await _audit.RegistrarAsync("CREAR", "Expense", gasto.Id.ToString(), usuarioId,
            new { gasto.Concepto, gasto.Importe });

        return MapToDto(gasto, null);
    }

    public async Task<List<ExpenseDto>> ListarDelDiaAsync(Guid usuarioId)
    {
        var scope = EmployeeScope.DeHoy(usuarioId, _fechas);

        var gastos = await _context.Expenses
            .Where(e => e.UsuarioId == scope.UsuarioId
                     && e.FechaHora >= scope.InicioUtc
                     && e.FechaHora <  scope.FinUtc)
            .OrderByDescending(e => e.FechaHora)
            .ToListAsync();

        var reportes = await EstadosDeReporteAsync(gastos.Select(g => g.Id).ToList());

        return gastos.Select(g => MapToDto(g, reportes.GetValueOrDefault(g.Id))).ToList();
    }

    // ── Admin: sin restricción de dueño ni de fecha ───────────────
    public async Task<PagedResultDto<ExpenseDto>> ListarAdminAsync(
        Guid? usuarioId, DateOnly? desde, DateOnly? hasta, int page, int size)
    {
        var query = _context.Expenses.Include(e => e.Usuario).AsQueryable();

        if (usuarioId.HasValue)
            query = query.Where(e => e.UsuarioId == usuarioId.Value);
        if (desde.HasValue)
            query = query.Where(e => e.FechaHora >= _fechas.InicioDelDiaUtc(desde.Value));
        if (hasta.HasValue)
            query = query.Where(e => e.FechaHora < _fechas.InicioDelDiaUtc(hasta.Value.AddDays(1)));

        var total = await query.CountAsync();
        var suma  = total == 0 ? 0m : await query.SumAsync(e => e.Importe);

        var items = await query
            .OrderByDescending(e => e.FechaHora)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        var reportes = await EstadosDeReporteAsync(items.Select(e => e.Id).ToList());

        return new PagedResultDto<ExpenseDto>
        {
            Items = items.Select(e =>
            {
                var dto = MapToDto(e, reportes.GetValueOrDefault(e.Id));
                dto.Empleado = e.Usuario?.Nombre;
                return dto;
            }).ToList(),
            Total        = total,
            Pagina       = page,
            TamanoPagina = size,
            TotalPaginas = (int)Math.Ceiling(total / (double)size),
            SumaTotal    = suma
        };
    }

    // ── Helpers ───────────────────────────────────────────────────
    private Task<bool> HayCorteCerradoAsync(EmployeeScope scope)
        => _context.CashCuts.AnyAsync(c => c.UsuarioId == scope.UsuarioId
                                        && c.FechaLocal == scope.Dia
                                        && c.Estado != "ANULADO");

    /// <summary>Último estado de reporte por registro, para marcar en la lista
    /// cuáles ya fueron reportados y no ofrecer reportarlos otra vez.</summary>
    private async Task<Dictionary<Guid, string>> EstadosDeReporteAsync(List<Guid> gastoIds)
    {
        if (gastoIds.Count == 0) return [];

        return await _context.ErrorReports
            .Where(r => r.TipoRegistro == "GASTO" && gastoIds.Contains(r.RegistroId))
            .GroupBy(r => r.RegistroId)
            .Select(g => new
            {
                RegistroId = g.Key,
                Estado     = g.OrderByDescending(r => r.FechaHora).First().Estado
            })
            .ToDictionaryAsync(x => x.RegistroId, x => x.Estado);
    }

    private static ExpenseDto MapToDto(Expense e, string? estadoReporte) => new()
    {
        Id              = e.Id,
        FechaHora       = e.FechaHora,
        Concepto        = e.Concepto,
        Categoria       = e.Categoria,
        Importe         = e.Importe,
        Estado          = e.Estado,
        IncluidoEnCorte = e.CashCutId.HasValue,
        EstadoReporte   = estadoReporte
    };
}
