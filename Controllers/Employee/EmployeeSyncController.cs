using FloreriaBautista.Data;
using FloreriaBautista.Extensions;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.QuickSale;
using FloreriaBautista.Models.DTOs.Sync;
using FloreriaBautista.Services;
using FloreriaBautista.Services.Employee;
using FloreriaBautista.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Controllers.Employee;

[ApiController]
[Tags("2. Operaciones y Ventas")]
[Route("api/employee")]
[Authorize(Roles = "ADMIN,EMPLEADO")]
public class EmployeeSyncController : ControllerBase
{
    private readonly IProductService _products;
    private readonly IOrderService   _orders;
    private readonly IFechaHelper    _fechas;
    private readonly AppDbContext     _context;

    public EmployeeSyncController(
        IProductService products,
        IOrderService   orders,
        IFechaHelper    fechas,
        AppDbContext     context)
    {
        _products = products;
        _orders   = orders;
        _fechas   = fechas;
        _context  = context;
    }

    /// <summary>
    /// Endpoint consolidado de sync para la app móvil del empleado.
    /// Sin <c>desde</c>: full sync (catálogo completo + registros del día).
    /// Con <c>desde</c>: delta sync (solo lo modificado desde esa marca).
    /// </summary>
    [HttpGet("sync")]
    public async Task<IActionResult> Sync([FromQuery] DateTime? desde)
    {
        var userId = User.UsuarioIdRequerido();
        var sincronizadoEn = DateTime.UtcNow;
        var esDelta = desde.HasValue;

        var resultado = new EmployeeSyncResponseDto
        {
            SincronizadoEn = sincronizadoEn.ToString("o"),
            EsDelta        = esDelta
        };

        if (esDelta)
        {
            var desdeUtc = FechaHelper.DesdeDelta(desde);

            var productsDelta = await _products.ListarDeltaAsync(desdeUtc);
            resultado.Productos = productsDelta.Items;

            var ordersDelta = await _orders.ListarDeltaEmpleadoAsync(userId, desdeUtc);
            resultado.Ventas = ordersDelta.Items;

            resultado.Plantillas = await _context.QuickSaleTemplates
                .Include(t => t.Items).ThenInclude(i => i.Product)
                .Where(t => t.ActualizadoEn > desdeUtc)
                .OrderBy(t => t.Orden).ThenBy(t => t.CreadoEn)
                .Select(t => new QuickSaleTemplateDto
                {
                    Id          = t.Id,
                    Nombre      = t.Nombre,
                    Descripcion = t.Descripcion,
                    Icono       = t.Icono,
                    Orden       = t.Orden,
                    Activa      = t.Activa,
                    Items       = t.Items.OrderBy(i => i.Orden).Select(i => new QuickSaleTemplateItemDto
                    {
                        Id        = i.Id,
                        ProductId = i.ProductId,
                        Nombre    = i.Product!.Nombre,
                        Precio    = i.Product.PrecioBase,
                        Icono     = i.Icono,
                        Color     = i.Color,
                        Cantidad  = i.CantidadPreset
                    }).ToList()
                })
                .ToListAsync();

            resultado.Gastos = await _context.Expenses
                .Where(e => e.UsuarioId == userId && e.ActualizadoEn > desdeUtc)
                .OrderByDescending(e => e.FechaHora)
                .Select(e => new ExpenseSyncDto
                {
                    Id              = e.Id,
                    FechaHora       = e.FechaHora,
                    Concepto        = e.Concepto,
                    Categoria       = e.Categoria,
                    Importe         = e.Importe,
                    Estado          = e.Estado,
                    IncluidoEnCorte = e.CashCutId.HasValue,
                    EstadoReporte   = null,
                    ActualizadoEn   = e.ActualizadoEn
                })
                .ToListAsync();

            resultado.Reportes = await _context.ErrorReports
                .Where(r => r.UsuarioId == userId && r.ActualizadoEn > desdeUtc)
                .OrderByDescending(r => r.FechaHora)
                .Select(r => new ErrorReportSyncDto
                {
                    Id               = r.Id,
                    TipoRegistro     = r.TipoRegistro,
                    RegistroId       = r.RegistroId,
                    Motivo           = r.Motivo,
                    Estado           = r.Estado,
                    FechaHora        = r.FechaHora,
                    FechaResolucion  = r.FechaResolucion,
                    ResolucionAccion = r.ResolucionAccion,
                    ResolucionNota   = r.ResolucionNota,
                    ActualizadoEn    = r.ActualizadoEn
                })
                .ToListAsync();
        }
        else
        {
            var scope = EmployeeScope.DeHoy(userId, _fechas);

            var allProducts = await _products.ListarDeltaAsync(DateTime.MinValue);
            resultado.Productos = allProducts.Items;

            resultado.Plantillas = await _context.QuickSaleTemplates
                .Include(t => t.Items).ThenInclude(i => i.Product)
                .Where(t => t.Activa)
                .OrderBy(t => t.Orden).ThenBy(t => t.CreadoEn)
                .Select(t => new QuickSaleTemplateDto
                {
                    Id          = t.Id,
                    Nombre      = t.Nombre,
                    Descripcion = t.Descripcion,
                    Icono       = t.Icono,
                    Orden       = t.Orden,
                    Activa      = t.Activa,
                    Items       = t.Items.OrderBy(i => i.Orden).Select(i => new QuickSaleTemplateItemDto
                    {
                        Id        = i.Id,
                        ProductId = i.ProductId,
                        Nombre    = i.Product!.Nombre,
                        Precio    = i.Product.PrecioBase,
                        Icono     = i.Icono,
                        Color     = i.Color,
                        Cantidad  = i.CantidadPreset
                    }).ToList()
                })
                .ToListAsync();

            resultado.Ventas = (await _orders.ListarDeltaEmpleadoAsync(userId, scope.InicioUtc)).Items;

            resultado.Gastos = await _context.Expenses
                .Where(e => e.UsuarioId == scope.UsuarioId
                         && e.FechaHora >= scope.InicioUtc
                         && e.FechaHora < scope.FinUtc)
                .OrderByDescending(e => e.FechaHora)
                .Select(e => new ExpenseSyncDto
                {
                    Id              = e.Id,
                    FechaHora       = e.FechaHora,
                    Concepto        = e.Concepto,
                    Categoria       = e.Categoria,
                    Importe         = e.Importe,
                    Estado          = e.Estado,
                    IncluidoEnCorte = e.CashCutId.HasValue,
                    EstadoReporte   = null,
                    ActualizadoEn   = e.ActualizadoEn
                })
                .ToListAsync();

            resultado.Reportes = await _context.ErrorReports
                .Where(r => r.UsuarioId == scope.UsuarioId &&
                            (r.Estado == "PENDIENTE" || r.Estado == "EN_REVISION" ||
                             r.FechaResolucion >= scope.InicioUtc))
                .OrderByDescending(r => r.FechaHora)
                .Select(r => new ErrorReportSyncDto
                {
                    Id               = r.Id,
                    TipoRegistro     = r.TipoRegistro,
                    RegistroId       = r.RegistroId,
                    Motivo           = r.Motivo,
                    Estado           = r.Estado,
                    FechaHora        = r.FechaHora,
                    FechaResolucion  = r.FechaResolucion,
                    ResolucionAccion = r.ResolucionAccion,
                    ResolucionNota   = r.ResolucionNota,
                    ActualizadoEn    = r.ActualizadoEn
                })
                .ToListAsync();
        }

        return Ok(ApiResponseDto<EmployeeSyncResponseDto>.Ok(resultado));
    }
}
