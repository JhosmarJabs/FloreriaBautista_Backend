namespace FloreriaBautista.Models.DTOs.Cms;

public class SeccionLegalDto
{
    public string Titulo { get; set; } = string.Empty;
    public string Cuerpo { get; set; } = string.Empty;
}

/// <summary>Documento tal como lo ve el visitante: sin notas de revisión ni bandera de provisional.</summary>
public class DocumentoLegalPublicoDto
{
    public string Slug   { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public List<SeccionLegalDto> Secciones { get; set; } = [];
    public DateTime ActualizadoEn { get; set; }
}

public class DocumentoLegalAdminDto : DocumentoLegalPublicoDto
{
    public string? NotasRevision           { get; set; }
    public bool    EsProvisional           { get; set; }
    public Guid?   ActualizadoPorUsuarioId { get; set; }
    public string? ActualizadoPorNombre    { get; set; }
}

public class ContenidoLegalPublicoDto
{
    public List<DocumentoLegalPublicoDto> Documentos { get; set; } = [];
}

public class ActualizarDocumentoLegalDto
{
    public string Titulo { get; set; } = string.Empty;
    public List<SeccionLegalDto> Secciones { get; set; } = [];
    public string? NotasRevision { get; set; }
    public bool    EsProvisional { get; set; }
}
