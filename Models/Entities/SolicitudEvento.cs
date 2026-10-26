namespace FloreriaBautista.Models.Entities;

/// <summary>Solicitud de cotización de un evento social enviada por un cliente desde /eventos.</summary>
public class SolicitudEvento
{
    public Guid     Id               { get; set; }
    public Guid     CustomerId       { get; set; }
    public string   TipoEvento       { get; set; } = string.Empty;
    public DateOnly FechaEvento      { get; set; }
    public int?     InvitadosAprox   { get; set; }
    public string   NombreContacto   { get; set; } = string.Empty;
    public string   TelefonoContacto { get; set; } = string.Empty;
    public string?  CorreoContacto   { get; set; }
    public string?  Mensaje          { get; set; }
    public string   Estado           { get; set; } = EstadosSolicitudEvento.Pendiente;
    public DateTime CreadoEn         { get; set; }
    public DateTime ActualizadoEn    { get; set; }

    public Customer? Customer { get; set; }
}

/// <summary>Catálogo de tipos de evento; la BD lo hace cumplir con <c>chk_solicitud_evento_tipo</c>.</summary>
public static class TiposEventoSolicitud
{
    public const string Boda          = "BODA";
    public const string Bautizo       = "BAUTIZO";
    public const string Corporativo   = "CORPORATIVO";
    public const string Conmemorativo = "CONMEMORATIVO";
    public const string Otro          = "OTRO";

    public static readonly string[] Todos = [Boda, Bautizo, Corporativo, Conmemorativo, Otro];
}

/// <summary>Catálogo de estados; la BD lo hace cumplir con <c>chk_solicitud_evento_estado</c>.</summary>
public static class EstadosSolicitudEvento
{
    public const string Pendiente  = "PENDIENTE";
    public const string Contactada = "CONTACTADA";
    public const string Cerrada    = "CERRADA";

    public static readonly string[] Todos = [Pendiente, Contactada, Cerrada];
}
