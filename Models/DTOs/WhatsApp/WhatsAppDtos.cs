namespace FloreriaBautista.Models.DTOs.WhatsApp;

public class WhatsAppEstadoDto
{
    public bool      Configurado          { get; set; }
    public bool      Habilitado           { get; set; }
    public string    Estado               { get; set; } = "NO_CONFIGURADO";
    public string?   NumeroVinculado      { get; set; }
    public DateTime? UltimaVerificacionEn { get; set; }
    public DateTime? DesconectadoDesde    { get; set; }
}

public class WhatsAppVincularDto
{
    public string? QrBase64         { get; set; }
    public string? PairingCode      { get; set; }
    public int     ExpiraEnSegundos { get; set; } = 30;
}

public class WhatsAppPruebaRequestDto
{
    public Guid? RecipientId { get; set; }
}

public class WhatsAppPruebaResultadoDto
{
    public int Encolados { get; set; }
}

public class WhatsAppConfiguracionDto
{
    public bool         Habilitado                { get; set; }
    public bool         NotificarPedidoNuevo      { get; set; }
    public bool         NotificarCambioEstado     { get; set; }
    public List<string> EstadosNotificados        { get; set; } = [];
    public bool         NotificarStockBajo        { get; set; }
    public bool         NotificarVentaInstantanea { get; set; }
    public bool         NotificarReabastecimiento { get; set; }
}

public class WhatsAppDestinatarioDto
{
    public Guid    Id                  { get; set; }
    public string  Nombre              { get; set; } = string.Empty;
    public string  TelefonoEnmascarado { get; set; } = string.Empty;
    /// <summary>Solo en el detalle (GET destinatarios/{id}).</summary>
    public string? Telefono            { get; set; }
    public bool    Activo              { get; set; }
    public bool?   Verificado          { get; set; }
}

public class WhatsAppDestinatarioRequestDto
{
    public string Nombre   { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public bool?  Activo   { get; set; }
}

public class WhatsAppVersionSugeridaDto
{
    public string? Version { get; set; }
    public string  Fuente  { get; set; } = "web.whatsapp.com";
}

public class WhatsAppMensajeDto
{
    public DateTime  CreadoEn     { get; set; }
    public DateTime? EnviadoEn    { get; set; }
    public string    TipoEvento   { get; set; } = string.Empty;
    public string    Destinatario { get; set; } = string.Empty;
    public string    Estado       { get; set; } = string.Empty;
    public int       Intentos     { get; set; }
    public string?   UltimoError  { get; set; }
}
