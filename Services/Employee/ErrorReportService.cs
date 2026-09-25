using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Employee;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Services.Employee;

/// <summary>
/// El empleado no edita ni borra: reporta. Este servicio es la única puerta que
/// tiene para pedir que se corrija algo que ya registró, y no toca el registro
/// original — solo lo marca para que el administrador decida.
/// </summary>
public class ErrorReportService : IErrorReportService
{
    private static readonly string[] TiposValidos    = ["VENTA", "GASTO", "CORTE"];
    private static readonly string[] AccionesValidas =
        ["CORREGIDO", "CANCELADO", "INVALIDADO", "ELIMINADO", "SIN_CAMBIO"];

    /// <summary>Acciones que dejan el registro sin efecto contable.</summary>
    private static readonly string[] AccionesQueAnulan = ["CANCELADO", "INVALIDADO", "ELIMINADO"];

    private readonly AppDbContext  _context;
    private readonly IFechaHelper  _fechas;
    private readonly IAuditService _audit;

    public ErrorReportService(AppDbContext context, IFechaHelper fechas, IAuditService audit)
    {
        _context = context;
        _fechas  = fechas;
        _audit   = audit;
    }

    public async Task<ErrorReportDto> CrearAsync(Guid usuarioId, CreateErrorReportRequestDto request)
    {
        var tipo  = request.TipoRegistro.Trim().ToUpperInvariant();
        if (!TiposValidos.Contains(tipo))
            throw new AppException($"Tipo de registro inválido. Usa: {string.Join(", ", TiposValidos)}.");

        var scope = EmployeeScope.DeHoy(usuarioId, _fechas);

        // Verificar que el registro exista, sea suyo y caiga en su día. Si algo
        // de eso falla se responde "no encontrado" y no "no autorizado": decir
        // que el registro existe pero es de otro ya es filtrar información.
        if (!await RegistroEsSuyoAsync(tipo, request.RegistroId, scope))
            throw new NotFoundException("Registro", request.RegistroId);

        // Un segundo reporte sobre lo mismo no aporta nada y multiplica la
        // bandeja del admin. Se bloquea mientras el primero siga abierto.
        var abierto = await _context.ErrorReports.AnyAsync(r =>
            r.TipoRegistro == tipo &&
            r.RegistroId   == request.RegistroId &&
            (r.Estado == "PENDIENTE" || r.Estado == "EN_REVISION"));

        if (abierto)
            throw new AppException("Ya hay un reporte abierto sobre este registro; espera la respuesta del administrador.");

        var reporte = new ErrorReport
        {
            Id           = Guid.NewGuid(),
            UsuarioId    = usuarioId,
            TipoRegistro = tipo,
            RegistroId   = request.RegistroId,
            Motivo       = request.Motivo.Trim(),
            Estado       = "PENDIENTE",
            FechaHora    = DateTime.UtcNow
        };

        _context.ErrorReports.Add(reporte);
        await _context.SaveChangesAsync();

        await _audit.RegistrarAsync("REPORTAR", "ErrorReport", reporte.Id.ToString(), usuarioId,
            new { reporte.TipoRegistro, reporte.RegistroId, reporte.Motivo });

        return MapToDto(reporte);
    }

    public async Task<List<ErrorReportDto>> ListarMisPendientesAsync(Guid usuarioId)
    {
        var scope = EmployeeScope.DeHoy(usuarioId, _fechas);

        // Los pendientes se muestran aunque sean de días anteriores: es la única
        // excepción a la regla del día actual, y es deliberada. Un reporte es una
        // petición abierta del propio empleado, no información operativa del
        // pasado, y ocultárselo lo dejaría sin saber si el admin ya le respondió.
        var reportes = await _context.ErrorReports
            .Where(r => r.UsuarioId == scope.UsuarioId &&
                        (r.Estado == "PENDIENTE" || r.Estado == "EN_REVISION" ||
                         r.FechaResolucion >= scope.InicioUtc))
            .OrderByDescending(r => r.FechaHora)
            .ToListAsync();

        return reportes.Select(MapToDto).ToList();
    }

    // ── Admin ─────────────────────────────────────────────────────
    public async Task<PagedResultDto<ErrorReportDto>> ListarAdminAsync(string? estado, int page, int size)
    {
        var query = _context.ErrorReports.Include(r => r.Usuario).AsQueryable();

        if (!string.IsNullOrWhiteSpace(estado))
            query = query.Where(r => r.Estado == estado.Trim().ToUpper());

        var total = await query.CountAsync();

        var items = await query
            .OrderBy(r => r.Estado == "PENDIENTE" ? 0 : 1)
            .ThenByDescending(r => r.FechaHora)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return new PagedResultDto<ErrorReportDto>
        {
            Items = items.Select(r =>
            {
                var dto = MapToDto(r);
                dto.Empleado = r.Usuario?.Nombre;
                return dto;
            }).ToList(),
            Total        = total,
            Pagina       = page,
            TamanoPagina = size,
            TotalPaginas = (int)Math.Ceiling(total / (double)size)
        };
    }

    public async Task<ErrorReportDto> ResolverAsync(
        Guid reporteId, Guid adminId, ResolveErrorReportRequestDto request)
    {
        var accion = request.Accion.Trim().ToUpperInvariant();
        if (!AccionesValidas.Contains(accion))
            throw new AppException($"Acción inválida. Usa: {string.Join(", ", AccionesValidas)}.");

        var reporte = await _context.ErrorReports.FindAsync(reporteId)
            ?? throw new NotFoundException("Reporte de error", reporteId);

        if (reporte.Estado is "RESUELTO" or "RECHAZADO")
            throw new AppException("Este reporte ya fue resuelto.");

        reporte.Estado               = request.Rechazar ? "RECHAZADO" : "RESUELTO";
        reporte.ResolucionAccion     = accion;
        reporte.ResolucionNota       = request.Nota?.Trim();
        reporte.ResueltoPorUsuarioId = adminId;
        reporte.FechaResolucion      = DateTime.UtcNow;

        if (!request.Rechazar && AccionesQueAnulan.Contains(accion))
            await AnularRegistroAsync(reporte, adminId);

        await _context.SaveChangesAsync();

        await _audit.RegistrarAsync("RESOLVER", "ErrorReport", reporte.Id.ToString(), adminId,
            new { reporte.TipoRegistro, reporte.RegistroId, reporte.Estado, Accion = accion, request.Nota });

        return MapToDto(reporte);
    }

    // ── Helpers ───────────────────────────────────────────────────
    private async Task<bool> RegistroEsSuyoAsync(string tipo, Guid registroId, EmployeeScope scope)
        => tipo switch
        {
            // Una venta es reportable si el empleado la capturó hoy o si se
            // entrega hoy: son exactamente los pedidos que puede ver.
            "VENTA" => await _context.Orders.AnyAsync(o =>
                o.Id == registroId &&
                o.AtendidoPorUsuarioId == scope.UsuarioId &&
                ((o.FechaCreacion >= scope.InicioUtc && o.FechaCreacion < scope.FinUtc) ||
                 o.FechaEntrega == scope.Dia)),

            "GASTO" => await _context.Expenses.AnyAsync(e =>
                e.Id == registroId &&
                e.UsuarioId == scope.UsuarioId &&
                e.FechaHora >= scope.InicioUtc && e.FechaHora < scope.FinUtc),

            "CORTE" => await _context.CashCuts.AnyAsync(c =>
                c.Id == registroId &&
                c.UsuarioId == scope.UsuarioId &&
                c.FechaLocal == scope.Dia),

            _ => false
        };

    /// <summary>
    /// Deja el registro sin efecto contable. Ni siquiera con acción ELIMINADO se
    /// borra la fila: un registro económico que desaparece rompe la trazabilidad
    /// y deja descuadrado cualquier corte que ya lo contó. Se marca ANULADO y la
    /// bitácora conserva quién lo decidió y por qué.
    ///
    /// Las ventas no se tocan aquí: el pedido tiene su propia máquina de estados
    /// (ver OrderService.TransicionesPermitidas) y cancelarlo implica devolver
    /// inventario y revisar pagos. El admin lo hace desde el flujo de pedidos.
    /// </summary>
    private async Task AnularRegistroAsync(ErrorReport reporte, Guid adminId)
    {
        switch (reporte.TipoRegistro)
        {
            case "GASTO":
                var gasto = await _context.Expenses.FindAsync(reporte.RegistroId);
                if (gasto is not null)
                {
                    gasto.Estado = "ANULADO";
                    await _audit.RegistrarAsync("ANULAR", "Expense", gasto.Id.ToString(), adminId,
                        new { Motivo = reporte.Motivo, ReporteId = reporte.Id });
                }
                break;

            case "CORTE":
                var corte = await _context.CashCuts.FindAsync(reporte.RegistroId);
                if (corte is not null)
                {
                    corte.Estado = "ANULADO";
                    // Liberar los gastos que había sellado, para que el empleado
                    // pueda rehacer el corte sin perderlos.
                    var gastos = await _context.Expenses
                        .Where(e => e.CashCutId == corte.Id)
                        .ToListAsync();
                    foreach (var g in gastos) g.CashCutId = null;

                    await _audit.RegistrarAsync("ANULAR", "CashCut", corte.Id.ToString(), adminId,
                        new { Motivo = reporte.Motivo, ReporteId = reporte.Id, GastosLiberados = gastos.Count });
                }
                break;
        }
    }

    private static ErrorReportDto MapToDto(ErrorReport r) => new()
    {
        Id               = r.Id,
        TipoRegistro     = r.TipoRegistro,
        RegistroId       = r.RegistroId,
        Motivo           = r.Motivo,
        Estado           = r.Estado,
        FechaHora        = r.FechaHora,
        FechaResolucion  = r.FechaResolucion,
        ResolucionAccion = r.ResolucionAccion,
        ResolucionNota   = r.ResolucionNota
    };
}
