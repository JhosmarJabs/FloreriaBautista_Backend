using FloreriaBautista.Data;
using FloreriaBautista.Helpers;
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
    private readonly ILogger<CatalogosController> _logger;

    public CatalogosController(AppDbContext context, ILogger<CatalogosController> logger)
    {
        _context = context;
        _logger = logger;
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
        var resultado = new List<(Guid Id, string Nombre, string? Descripcion, string? ImagenUrl,
            string ProximoInicio, string ProximoFin, int DiasParaInicio, bool EnCurso)>();
        foreach (var c in catalogos)
        {
            // Un catalogo con ventana invalida (dato heredado) se omite, no tumba la respuesta.
            if (!MesDia.EsValido(c.MesDiaInicio) || !MesDia.EsValido(c.MesDiaFin))
            {
                _logger.LogWarning("Catalogo {CatalogoId} omitido en temporadas-proximas: ventana MM-DD invalida.", c.Id);
                continue;
            }
            var (proximoInicio, proximoFin) = CalcularProximaOcurrencia(
                c.MesDiaInicio!, c.MesDiaFin!, hoy);
            var diasFaltan = proximoInicio.DayNumber - DateOnly.FromDateTime(hoy).DayNumber;
            resultado.Add((c.Id, c.Nombre, c.Descripcion, c.ImagenUrl,
                proximoInicio.ToString("yyyy-MM-dd"), proximoFin.ToString("yyyy-MM-dd"),
                diasFaltan,
                diasFaltan <= 0 && DateOnly.FromDateTime(hoy) <= proximoFin));
        }
        var resultadoFinal = resultado
            .Select(x => new
            {
                x.Id, x.Nombre, x.Descripcion, x.ImagenUrl,
                ProximoInicio = x.ProximoInicio,
                ProximoFin = x.ProximoFin,
                DiasParaInicio = x.DiasParaInicio,
                EnCurso = x.EnCurso
            })
            .Where(x => x.DiasParaInicio <= 60 || x.EnCurso)
            .OrderBy(x => x.DiasParaInicio)
            .ToList();

        return Ok(resultadoFinal);
    }

    private static (DateOnly inicio, DateOnly fin) CalcularProximaOcurrencia(
        string mesDiaInicio, string mesDiaFin, DateTime ahora)
    {
        // Los llamadores ya validaron con MesDia.EsValido; aqui no se lanza por formato.
        MesDia.TryParse(mesDiaInicio, out var mesI, out var diaI);
        MesDia.TryParse(mesDiaFin, out var mesF, out var diaF);

        static DateOnly Fecha(int a, int m, int d) =>
            new DateOnly(a, m, Math.Min(d, DateTime.DaysInMonth(a, m)));

        var anio = ahora.Year;
        var hoyDate = DateOnly.FromDateTime(ahora);
        var inicio = Fecha(anio, mesI, diaI);
        var fin    = Fecha(anio, mesF, diaF);

        if (fin < inicio)
        {
            // La ventana cruza el 31 de diciembre: la ocurrencia que empezo el
            // año anterior (inicio año-1 -> fin año) puede contener a "hoy".
            var finPrev = fin;
            if (hoyDate <= finPrev)
                return (Fecha(anio - 1, mesI, diaI), finPrev);

            fin = Fecha(anio + 1, mesF, diaF);
        }

        if (fin < hoyDate)
        {
            inicio = Fecha(anio + 1, mesI, diaI);
            fin    = Fecha(anio + 1, mesF, diaF);
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

        request.MesDiaInicio = MesDia.Normalizar(request.MesDiaInicio);
        request.MesDiaFin = MesDia.Normalizar(request.MesDiaFin);
        if ((request.MesDiaInicio != null && !MesDia.EsValido(request.MesDiaInicio))
            || (request.MesDiaFin != null && !MesDia.EsValido(request.MesDiaFin)))
        {
            return BadRequest(new { mensaje = MesDia.MensajeInvalido });
        }

        request.Id = Guid.NewGuid();
        request.CreadoEn = DateTime.UtcNow;
        request.ActualizadoEn = DateTime.UtcNow;

        _context.Catalogos.Add(request);
        await _context.SaveChangesAsync();

        return Ok(request);
    }
}
