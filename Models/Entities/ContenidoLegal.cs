namespace FloreriaBautista.Models.Entities;

/// <summary>
/// Documento legal o de ayuda del sitio público (aviso de privacidad, términos, envíos,
/// cancelaciones y preguntas frecuentes). Una fila por documento; los slugs son fijos
/// (ver ContenidoLegalPorDefecto) y las filas que falten se crean solas con texto provisional.
/// </summary>
public class ContenidoLegal
{
    public int      Id                      { get; set; }
    public string   Slug                    { get; set; } = string.Empty;
    public string   Titulo                  { get; set; } = string.Empty;

    /// <summary>JSON: [{ "titulo": string, "cuerpo": string }]. Texto plano, sin HTML.</summary>
    public string   SeccionesJson           { get; set; } = "[]";

    /// <summary>Puntos por confirmar. Solo los ve el administrador.</summary>
    public string?  NotasRevision           { get; set; }
    public bool     EsProvisional           { get; set; } = true;
    public DateTime ActualizadoEn           { get; set; } = DateTime.UtcNow;
    public Guid?    ActualizadoPorUsuarioId { get; set; }
}
