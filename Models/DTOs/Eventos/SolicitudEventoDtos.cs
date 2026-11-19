namespace FloreriaBautista.Models.DTOs.Eventos;

/// <summary>Cuerpo del POST del cliente. No recibe CustomerId, Estado ni fechas de sistema: los fija el servidor.</summary>
public class CrearSolicitudEventoDto
{
    public string    TipoEvento       { get; set; } = string.Empty;
    public DateOnly  FechaEvento      { get; set; }
    public int?      InvitadosAprox   { get; set; }
    public string    NombreContacto   { get; set; } = string.Empty;
    public string    TelefonoContacto { get; set; } = string.Empty;
    public string?   CorreoContacto   { get; set; }
    public string?   Mensaje          { get; set; }
}

public class SolicitudEventoDto
{
    public Guid      Id               { get; set; }
    public Guid      CustomerId       { get; set; }
    public string    ClienteNombre    { get; set; } = string.Empty;
    public string    TipoEvento       { get; set; } = string.Empty;
    public DateOnly  FechaEvento      { get; set; }
    public int?      InvitadosAprox   { get; set; }
    public string    NombreContacto   { get; set; } = string.Empty;
    public string    TelefonoContacto { get; set; } = string.Empty;
    public string?   CorreoContacto   { get; set; }
    public string?   Mensaje          { get; set; }
    public string    Estado           { get; set; } = string.Empty;
    public DateTime  CreadoEn         { get; set; }
    public DateTime  ActualizadoEn    { get; set; }
}

public class CambiarEstadoSolicitudEventoDto
{
    public string Estado { get; set; } = string.Empty;
}
