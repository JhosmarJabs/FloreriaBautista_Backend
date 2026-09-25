using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Employee;

namespace FloreriaBautista.Services.Interfaces;

/// <summary>
/// Gastos de personal. Los métodos del empleado reciben su id y construyen el
/// filtro internamente; no aceptan rango de fechas ni id de otro usuario, para
/// que no exista una firma capaz de devolver datos ajenos aunque el controller
/// se equivoque.
/// </summary>
public interface IEmployeeExpenseService
{
    Task<ExpenseDto>       RegistrarAsync(Guid usuarioId, CreateExpenseRequestDto request);
    Task<List<ExpenseDto>> ListarDelDiaAsync(Guid usuarioId);

    // ── Admin ──────────────────────────────────────────────────────
    Task<PagedResultDto<ExpenseDto>> ListarAdminAsync(
        Guid? usuarioId, DateOnly? desde, DateOnly? hasta, int page, int size);
}

public interface ICashCutService
{
    /// <summary>Los números del día ANTES de cerrar, para que el empleado cuadre.</summary>
    Task<CashCutPreviewDto> PreviewAsync(Guid usuarioId);

    Task<CashCutDto>        CerrarAsync(Guid usuarioId, CreateCashCutRequestDto request);

    /// <summary>El corte de hoy del empleado, o null si todavía no cierra.</summary>
    Task<CashCutDto?>       ObtenerDelDiaAsync(Guid usuarioId);

    // ── Admin ──────────────────────────────────────────────────────
    Task<PagedResultDto<CashCutDto>> ListarAdminAsync(
        Guid? usuarioId, DateOnly? desde, DateOnly? hasta, int page, int size);
}

public interface IErrorReportService
{
    Task<ErrorReportDto>       CrearAsync(Guid usuarioId, CreateErrorReportRequestDto request);

    /// <summary>Los reportes que el empleado levantó hoy y siguen sin resolverse.</summary>
    Task<List<ErrorReportDto>> ListarMisPendientesAsync(Guid usuarioId);

    // ── Admin ──────────────────────────────────────────────────────
    Task<PagedResultDto<ErrorReportDto>> ListarAdminAsync(string? estado, int page, int size);
    Task<ErrorReportDto>                 ResolverAsync(Guid reporteId, Guid adminId, ResolveErrorReportRequestDto request);
}
