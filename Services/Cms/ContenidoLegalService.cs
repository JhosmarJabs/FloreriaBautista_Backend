using System.Text.Json;
using System.Text.RegularExpressions;
using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Cms;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Services.Cms;

/// <summary>
/// Páginas legales y de ayuda editables desde el CMS. Los 5 documentos son fijos
/// (ContenidoLegalPorDefecto.Slugs): se editan, no se crean ni se borran. Las filas que falten
/// se crean de forma perezosa con el texto provisional.
/// </summary>
public class ContenidoLegalService
{
    public const int TituloMin        = 3;
    public const int TituloMax        = 150;
    public const int MaxSecciones     = 40;
    public const int SeccionTituloMax = 200;
    public const int CuerpoMax        = 8_000;
    public const int CuerposTotalMax  = 60_000;
    public const int NotasMax         = 4_000;

    private static readonly Regex VariableRegex = new(@"\{\{\s*([^{}]*?)\s*\}\}", RegexOptions.Compiled);

    private readonly AppDbContext  _context;
    private readonly IAuditService _audit;

    public ContenidoLegalService(AppDbContext context, IAuditService audit)
    {
        _context = context;
        _audit   = audit;
    }

    // ── Lectura ───────────────────────────────────────────────────

    public async Task<ContenidoLegalPublicoDto> ObtenerPublicoAsync()
    {
        var filas = await AsegurarDocumentosAsync();
        return new ContenidoLegalPublicoDto { Documentos = filas.Select(MapPublico).ToList() };
    }

    public async Task<List<DocumentoLegalAdminDto>> ObtenerAdminAsync()
    {
        var filas = await AsegurarDocumentosAsync();
        var nombres = await ObtenerNombresAsync(filas);
        return filas.Select(f => MapAdmin(f, nombres)).ToList();
    }

    // ── Escritura ─────────────────────────────────────────────────

    public async Task<DocumentoLegalAdminDto> ActualizarAsync(string slug, ActualizarDocumentoLegalDto dto, Guid? usuarioId)
    {
        // Comparación exacta: un slug en mayúsculas o con espacios tampoco existe.
        if (!ContenidoLegalPorDefecto.Slugs.Contains(slug, StringComparer.Ordinal))
            throw new NotFoundException("Página legal", slug);

        var filas = await AsegurarDocumentosAsync();
        var fila  = filas.First(f => f.Slug == slug);

        var titulo = (dto.Titulo ?? string.Empty).Trim();
        var notas  = Normalizar((dto.NotasRevision ?? string.Empty).Trim());
        var secciones = LimpiarSecciones(dto.Secciones);
        Validar(titulo, secciones, notas);

        fila.Titulo        = titulo;
        fila.SeccionesJson = JsonSerializer.Serialize(secciones, ContenidoLegalPorDefecto.JsonOpciones);
        fila.NotasRevision = notas.Length == 0 ? null : notas;
        fila.EsProvisional = dto.EsProvisional;
        fila.ActualizadoEn = DateTime.UtcNow;
        fila.ActualizadoPorUsuarioId = usuarioId;
        await _context.SaveChangesAsync();

        // Sin el texto completo en la auditoría.
        await _audit.RegistrarAsync("CMS_LEGAL_ACTUALIZAR", "ContenidoLegal", slug, usuarioId,
            new { titulo, secciones = secciones.Count, esProvisional = dto.EsProvisional });

        var nombres = await ObtenerNombresAsync([fila]);
        return MapAdmin(fila, nombres);
    }

    // ── Creación perezosa ─────────────────────────────────────────

    /// <summary>Devuelve los 5 documentos en el orden de Slugs, creando solo los que falten.</summary>
    private async Task<List<ContenidoLegal>> AsegurarDocumentosAsync()
    {
        var existentes = await _context.ContenidoLegal.ToListAsync();
        var faltantes  = ContenidoLegalPorDefecto.Slugs
            .Where(s => existentes.All(e => e.Slug != s))
            .ToList();

        if (faltantes.Count > 0)
        {
            foreach (var slug in faltantes)
                _context.ContenidoLegal.Add(ContenidoLegalPorDefecto.Crear(slug));
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Otra petición insertó a la vez (índice único por slug): se descarta lo propio
                // y se vuelve a leer. Nunca se sobrescribe una fila existente.
                _context.ChangeTracker.Clear();
            }
            existentes = await _context.ContenidoLegal.ToListAsync();
        }

        return ContenidoLegalPorDefecto.Slugs
            .Select(s => existentes.FirstOrDefault(e => e.Slug == s))
            .Where(e => e is not null)
            .Select(e => e!)
            .ToList();
    }

    // ── Validación ────────────────────────────────────────────────

    private static List<SeccionLegalDto> LimpiarSecciones(List<SeccionLegalDto>? secciones) =>
        (secciones ?? [])
            .Select(s => new SeccionLegalDto
            {
                Titulo = Normalizar((s?.Titulo ?? string.Empty).Trim()),
                Cuerpo = Normalizar((s?.Cuerpo ?? string.Empty).Trim())
            })
            .Where(s => s.Titulo.Length > 0 || s.Cuerpo.Length > 0)
            .ToList();

    private static string Normalizar(string texto) => texto.Replace("\r\n", "\n").Replace('\r', '\n');

    private static void Validar(string titulo, List<SeccionLegalDto> secciones, string notas)
    {
        if (titulo.Length < TituloMin || titulo.Length > TituloMax)
            throw new AppException($"El título es obligatorio y debe tener entre {TituloMin} y {TituloMax} caracteres.");
        if (secciones.Count > MaxSecciones)
            throw new AppException($"Una página puede tener como máximo {MaxSecciones} secciones.");
        if (notas.Length > NotasMax)
            throw new AppException($"Las notas de revisión no pueden pasar de {NotasMax} caracteres.");

        ValidarVariables(titulo);
        var total = 0;
        for (var i = 0; i < secciones.Count; i++)
        {
            var s = secciones[i];
            var n = i + 1;
            if (s.Titulo.Length == 0)
                throw new AppException($"La sección {n} necesita un título.");
            if (s.Titulo.Length > SeccionTituloMax)
                throw new AppException($"El título de la sección {n} no puede pasar de {SeccionTituloMax} caracteres.");
            if (s.Cuerpo.Length == 0)
                throw new AppException($"La sección {n} necesita contenido.");
            if (s.Cuerpo.Length > CuerpoMax)
                throw new AppException($"El contenido de la sección {n} no puede pasar de {CuerpoMax} caracteres.");
            total += s.Cuerpo.Length;
            ValidarVariables(s.Titulo);
            ValidarVariables(s.Cuerpo);
        }
        if (total > CuerposTotalMax)
            throw new AppException($"El contenido total no puede pasar de {CuerposTotalMax} caracteres.");
    }

    private static void ValidarVariables(string texto)
    {
        foreach (Match m in VariableRegex.Matches(texto))
        {
            var nombre = m.Groups[1].Value;
            if (!ContenidoLegalPorDefecto.Variables.Contains(nombre, StringComparer.Ordinal))
                throw new AppException("Variable no reconocida: {{" + nombre + "}}");
        }
    }

    // ── Mapeo ─────────────────────────────────────────────────────

    private static List<SeccionLegalDto> LeerSecciones(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<SeccionLegalDto>>(json, ContenidoLegalPorDefecto.JsonOpciones) ?? []; }
        catch (JsonException) { return []; }
    }

    private static DocumentoLegalPublicoDto MapPublico(ContenidoLegal f) => new()
    {
        Slug          = f.Slug,
        Titulo        = f.Titulo,
        Secciones     = LeerSecciones(f.SeccionesJson),
        ActualizadoEn = DateTime.SpecifyKind(f.ActualizadoEn, DateTimeKind.Utc)
    };

    private static DocumentoLegalAdminDto MapAdmin(ContenidoLegal f, Dictionary<Guid, string> nombres) => new()
    {
        Slug          = f.Slug,
        Titulo        = f.Titulo,
        Secciones     = LeerSecciones(f.SeccionesJson),
        ActualizadoEn = DateTime.SpecifyKind(f.ActualizadoEn, DateTimeKind.Utc),
        NotasRevision = f.NotasRevision,
        EsProvisional = f.EsProvisional,
        ActualizadoPorUsuarioId = f.ActualizadoPorUsuarioId,
        ActualizadoPorNombre    = f.ActualizadoPorUsuarioId is { } id && nombres.TryGetValue(id, out var n) ? n : null
    };

    private async Task<Dictionary<Guid, string>> ObtenerNombresAsync(IEnumerable<ContenidoLegal> filas)
    {
        var ids = filas.Where(f => f.ActualizadoPorUsuarioId.HasValue)
                       .Select(f => f.ActualizadoPorUsuarioId!.Value)
                       .Distinct().ToList();
        if (ids.Count == 0) return [];
        var usuarios = await _context.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.Nombre, u.Apellido })
            .ToListAsync();
        return usuarios.ToDictionary(u => u.Id, u => $"{u.Nombre} {u.Apellido}".Trim());
    }
}
