using FloreriaBautista.Data;
using FloreriaBautista.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Controllers;

[ApiController]
[Tags("Público")]
[Route("api/catalogos")]
public class CatalogosController : ControllerBase
{
    private readonly AppDbContext _context;

    public CatalogosController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetCatalogos()
    {
        bool isAdmin = User.Identity?.IsAuthenticated == true && User.IsInRole("ADMIN");

        var query = _context.Catalogos.AsQueryable();

        if (!isAdmin)
        {
            query = query.Where(c => c.Activo && c.Estado == "ACTIVA");
        }

        var items = await query
            .OrderBy(c => c.Nombre)
            .Select(c => new 
            { 
                c.Id, 
                c.Nombre, 
                c.Descripcion,
                c.ImagenUrl,
                c.Activo,
                c.Estado
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCatalogo(Guid id)
    {
        var item = await _context.Catalogos.FindAsync(id);
        if (item == null)
        {
            return NotFound(new { mensaje = "Catálogo no encontrado." });
        }
        return Ok(item);
    }

    [HttpGet("temporadas-proximas")]
    public async Task<IActionResult> TemporadasProximas()
    {
        var catalogos = await _context.Catalogos
            .Where(c => c.Activo && c.Estado == "ACTIVA"
                && c.MesDiaInicio != null && c.MesDiaFin != null)
            .OrderBy(c => c.MesDiaInicio)
            .Select(c => new
            {
                c.Id, c.Nombre, c.Descripcion, c.ImagenUrl,
                c.MesDiaInicio, c.MesDiaFin
            })
            .ToListAsync();

        var hoy = DateTime.UtcNow;
        var resultado = catalogos
            .Select(c =>
            {
                var (proximoInicio, proximoFin) = CalcularProximaOcurrencia(
                    c.MesDiaInicio!, c.MesDiaFin!, hoy);
                var diasFaltan = proximoInicio.DayNumber - DateOnly.FromDateTime(hoy).DayNumber;
                return new
                {
                    c.Id, c.Nombre, c.Descripcion, c.ImagenUrl,
                    ProximoInicio = proximoInicio.ToString("yyyy-MM-dd"),
                    ProximoFin = proximoFin.ToString("yyyy-MM-dd"),
                    DiasParaInicio = diasFaltan,
                    EnCurso = diasFaltan <= 0
                        && DateOnly.FromDateTime(hoy) <= proximoFin
                };
            })
            .Where(x => x.DiasParaInicio <= 60 || x.EnCurso)
            .OrderBy(x => x.DiasParaInicio)
            .ToList();

        return Ok(resultado);
    }

    private static (DateOnly inicio, DateOnly fin) CalcularProximaOcurrencia(
        string mesDiaInicio, string mesDiaFin, DateTime ahora)
    {
        var partsI = mesDiaInicio.Split('-');
        var partsF = mesDiaFin.Split('-');
        int mesI = int.Parse(partsI[0]), diaI = int.Parse(partsI[1]);
        int mesF = int.Parse(partsF[0]), diaF = int.Parse(partsF[1]);

        var anio = ahora.Year;
        var inicio = new DateOnly(anio, mesI, Math.Min(diaI, DateTime.DaysInMonth(anio, mesI)));
        var fin    = new DateOnly(anio, mesF, Math.Min(diaF, DateTime.DaysInMonth(anio, mesF)));

        if (fin < inicio) fin = fin.AddYears(1);

        var hoyDate = DateOnly.FromDateTime(ahora);
        if (fin < hoyDate)
        {
            inicio = inicio.AddYears(1);
            fin    = fin.AddYears(1);
        }

        return (inicio, fin);
    }

    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Crear([FromBody] Catalogo request)
    {
        if (request == null)
        {
            return BadRequest(new { mensaje = "Los datos del catálogo son requeridos." });
        }

        request.Id = Guid.NewGuid();
        request.CreadoEn = DateTime.UtcNow;
        request.ActualizadoEn = DateTime.UtcNow;

        _context.Catalogos.Add(request);
        await _context.SaveChangesAsync();

        return Ok(request);
    }
}
