using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.WhatsApp;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Enums;
using FloreriaBautista.Services.Interfaces;
using FloreriaBautista.Services.WhatsApp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Controllers.Admin;

/// <summary>
/// Módulo de WhatsApp (Evolution API). Solo ADMIN. La URL y la API key de Evolution nunca salen
/// del backend; el QR y el código de emparejamiento no se guardan, no se auditan y no se loguean.
/// </summary>
[ApiController]
[Tags("3. Sistema y Seguridad")]
[Route("api/admin/whatsapp")]
[Authorize(Roles = "ADMIN")]
public class AdminWhatsAppController : ControllerBase
{
    /// <summary>Estados que se pueden notificar (sin NO_COMPLETADO ni las variantes en femenino).</summary>
    private static readonly string[] EstadosNotificables =
    [
        EstadosPedido.PendienteValidacion, EstadosPedido.EnPreparacion, EstadosPedido.EnRuta,
        EstadosPedido.PendienteAnulacion, EstadosPedido.Entregado, EstadosPedido.Cancelado
    ];

    private readonly AppDbContext              _context;
    private readonly IEvolutionClient          _evolution;
    private readonly IWhatsAppNotifier         _notifier;
    private readonly WhatsAppConnectionMonitor _monitor;
    private readonly WhatsAppVersionProvider   _versiones;
    private readonly WhatsAppConfig            _config;
    private readonly WhatsAppEstadoActual      _estadoActual;
    private readonly IAuditService             _audit;

    public AdminWhatsAppController(AppDbContext context, IEvolutionClient evolution,
        IWhatsAppNotifier notifier, WhatsAppConnectionMonitor monitor,
        WhatsAppVersionProvider versiones, WhatsAppConfig config, WhatsAppEstadoActual estadoActual,
        IAuditService audit)
    {
        _context   = context;
        _evolution = evolution;
        _notifier  = notifier;
        _monitor   = monitor;
        _versiones = versiones;
        _config    = config;
        _estadoActual = estadoActual;
        _audit     = audit;
    }

    // ── Conexión ──────────────────────────────────────────────────

    [HttpGet("estado")]
    public async Task<IActionResult> Estado()
    {
        var s = await ObtenerSettingsAsync();
        return Ok(ApiResponseDto<WhatsAppEstadoDto>.Ok(MapEstado(s)));
    }

    [HttpPost("vincular")]
    public async Task<IActionResult> Vincular(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";

        if (!_config.Configurado)
            return StatusCode(503, ApiResponseDto<WhatsAppVincularDto>.Fail("WhatsApp no está configurado en el servidor."));

        var estado = await _evolution.ObtenerEstadoAsync(ct);
        if (estado.Exito && WhatsAppConnectionMonitor.Traducir(estado.Valor) == WhatsAppConstantes.Conectado)
            return Conflict(ApiResponseDto<WhatsAppVincularDto>.Fail("Ya está vinculado; desvincula primero."));

        var creada = await _evolution.CrearInstanciaSiNoExisteAsync(ct);
        if (!creada.Exito) return NoDisponible<WhatsAppVincularDto>(creada.Mensaje);

        if (_config.WebhookUrl is not null && _config.WebhookToken is not null)
            await _evolution.ConfigurarWebhookAsync(_config.WebhookUrl, _config.WebhookToken, ct);

        var qr = await _evolution.ConectarAsync(ct);
        if (!qr.Exito) return NoDisponible<WhatsAppVincularDto>(qr.Mensaje);

        await _audit.RegistrarAsync("WHATSAPP_VINCULAR", "WhatsAppSettings", "1", UsuarioId(),
            new { instanciaCreada = creada.Valor, webhookConfigurado = _config.WebhookUrl is not null });

        return Ok(ApiResponseDto<WhatsAppVincularDto>.Ok(new WhatsAppVincularDto
        {
            QrBase64         = qr.Valor?.Base64,
            PairingCode      = qr.Valor?.PairingCode,
            ExpiraEnSegundos = 30
        }));
    }

    [HttpPost("desvincular")]
    public async Task<IActionResult> Desvincular(CancellationToken ct)
    {
        if (!_config.Configurado)
            return StatusCode(503, ApiResponseDto<object>.Fail("WhatsApp no está configurado en el servidor."));

        var r = await _evolution.DesconectarAsync(ct);
        if (!r.Exito && r.CodigoError != EvolutionCodigos.NoEncontrado)
            return NoDisponible<object>(r.Mensaje);

        await _monitor.RegistrarEstadoAsync(WhatsAppConstantes.Desconectado, "desvincular");
        // Una desvinculación voluntaria no es una caída: no debe generar el aviso.
        var s = await ObtenerSettingsAsync();
        s.DesconectadoDesde = null;
        s.AvisoDesconexionEnviado = false;
        await _context.SaveChangesAsync();

        await _audit.RegistrarAsync("WHATSAPP_DESVINCULAR", "WhatsAppSettings", "1", UsuarioId());
        return Ok(ApiResponseDto<object>.Ok(new { estado = s.EstadoConexion }));
    }

    [HttpPost("prueba")]
    public async Task<IActionResult> Prueba([FromBody] WhatsAppPruebaRequestDto? dto)
    {
        if (!_config.Configurado)
            return StatusCode(503, ApiResponseDto<WhatsAppPruebaResultadoDto>.Fail("WhatsApp no está configurado en el servidor."));

        var s = await ObtenerSettingsAsync();
        if (s.EstadoConexion != WhatsAppConstantes.Conectado)
            return Conflict(ApiResponseDto<WhatsAppPruebaResultadoDto>.Fail("WhatsApp no está conectado."));

        var encolados = await _notifier.EncolarPruebaAsync(dto?.RecipientId);
        if (encolados == 0)
            return BadRequest(ApiResponseDto<WhatsAppPruebaResultadoDto>.Fail("No hay destinatarios activos."));

        await _audit.RegistrarAsync("WHATSAPP_PRUEBA", "WhatsAppSettings", "1", UsuarioId(),
            new { recipientId = dto?.RecipientId, encolados });
        return Ok(ApiResponseDto<WhatsAppPruebaResultadoDto>.Ok(new WhatsAppPruebaResultadoDto { Encolados = encolados },
            "Mensaje de prueba encolado."));
    }

    // ── Configuración de eventos ─────────────────────────────────

    [HttpGet("configuracion")]
    public async Task<IActionResult> ObtenerConfiguracion()
    {
        var s = await ObtenerSettingsAsync();
        return Ok(ApiResponseDto<WhatsAppConfiguracionDto>.Ok(MapConfiguracion(s)));
    }

    [HttpPut("configuracion")]
    public async Task<IActionResult> ActualizarConfiguracion([FromBody] WhatsAppConfiguracionDto dto)
    {
        var estados = (dto.EstadosNotificados ?? [])
            .Select(e => e.Trim().ToUpperInvariant()).Where(e => e.Length > 0).Distinct().ToList();
        var desconocidos = estados.Where(e => !EstadosNotificables.Contains(e)).ToList();
        if (desconocidos.Count > 0)
            return BadRequest(ApiResponseDto<WhatsAppConfiguracionDto>.Fail(
                $"Estado desconocido: {string.Join(", ", desconocidos)}."));

        var s = await ObtenerSettingsAsync();
        s.Habilitado                = dto.Habilitado;
        s.NotificarPedidoNuevo      = dto.NotificarPedidoNuevo;
        s.NotificarCambioEstado     = dto.NotificarCambioEstado;
        s.EstadosNotificados        = string.Join(',', estados);
        s.NotificarStockBajo        = dto.NotificarStockBajo;
        s.NotificarVentaInstantanea = dto.NotificarVentaInstantanea;
        s.NotificarReabastecimiento = dto.NotificarReabastecimiento;
        s.ActualizadoEn             = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _audit.RegistrarAsync("WHATSAPP_CONFIG", "WhatsAppSettings", "1", UsuarioId(), MapConfiguracion(s));
        return Ok(ApiResponseDto<WhatsAppConfiguracionDto>.Ok(MapConfiguracion(s), "Configuración guardada."));
    }

    // ── Destinatarios ────────────────────────────────────────────

    [HttpGet("destinatarios")]
    public async Task<IActionResult> ListarDestinatarios()
    {
        var lista = await _context.WhatsAppRecipients.AsNoTracking()
            .OrderByDescending(r => r.Activo).ThenBy(r => r.Nombre)
            .ToListAsync();
        return Ok(ApiResponseDto<List<WhatsAppDestinatarioDto>>.Ok(lista.Select(r => MapDestinatario(r, false)).ToList()));
    }

    [HttpGet("destinatarios/{id:guid}")]
    public async Task<IActionResult> ObtenerDestinatario(Guid id)
    {
        var r = await _context.WhatsAppRecipients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (r is null) return NotFound(ApiResponseDto<WhatsAppDestinatarioDto>.Fail("Destinatario no encontrado."));
        return Ok(ApiResponseDto<WhatsAppDestinatarioDto>.Ok(MapDestinatario(r, true)));
    }

    [HttpPost("destinatarios")]
    public async Task<IActionResult> CrearDestinatario([FromBody] WhatsAppDestinatarioRequestDto dto, CancellationToken ct)
    {
        var (nombre, telefono, error) = Validar(dto);
        if (error is not null) return BadRequest(ApiResponseDto<WhatsAppDestinatarioDto>.Fail(error));

        if (await _context.WhatsAppRecipients.AnyAsync(r => r.Telefono == telefono, ct))
            return Conflict(ApiResponseDto<WhatsAppDestinatarioDto>.Fail("Ese teléfono ya está registrado."));

        var activo = dto.Activo ?? true;
        if (activo && await _context.WhatsAppRecipients.CountAsync(r => r.Activo, ct) >= WhatsAppConfig.MaxDestinatariosActivos)
            return UnprocessableEntity(ApiResponseDto<WhatsAppDestinatarioDto>.Fail(
                $"Máximo {WhatsAppConfig.MaxDestinatariosActivos} destinatarios activos."));

        var r = new WhatsAppRecipient
        {
            Id         = Guid.NewGuid(),
            Nombre     = nombre,
            Telefono   = telefono,
            Activo     = activo,
            Verificado = await VerificarAsync(telefono, ct)
        };
        _context.WhatsAppRecipients.Add(r);
        await _context.SaveChangesAsync(ct);

        await _audit.RegistrarAsync("WHATSAPP_DESTINATARIO_CREAR", "WhatsAppRecipient", r.Id.ToString(), UsuarioId(),
            new { r.Nombre, telefono = WhatsAppConstantes.Enmascarar(r.Telefono), r.Activo });
        return StatusCode(201, ApiResponseDto<WhatsAppDestinatarioDto>.Ok(MapDestinatario(r, false), MensajeVerificacion(r)));
    }

    [HttpPut("destinatarios/{id:guid}")]
    public async Task<IActionResult> ActualizarDestinatario(Guid id, [FromBody] WhatsAppDestinatarioRequestDto dto, CancellationToken ct)
    {
        var r = await _context.WhatsAppRecipients.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return NotFound(ApiResponseDto<WhatsAppDestinatarioDto>.Fail("Destinatario no encontrado."));

        var (nombre, telefono, error) = Validar(dto);
        if (error is not null) return BadRequest(ApiResponseDto<WhatsAppDestinatarioDto>.Fail(error));

        if (telefono != r.Telefono && await _context.WhatsAppRecipients.AnyAsync(x => x.Telefono == telefono && x.Id != id, ct))
            return Conflict(ApiResponseDto<WhatsAppDestinatarioDto>.Fail("Ese teléfono ya está registrado."));

        var activo = dto.Activo ?? r.Activo;
        if (activo && !r.Activo
            && await _context.WhatsAppRecipients.CountAsync(x => x.Activo, ct) >= WhatsAppConfig.MaxDestinatariosActivos)
            return UnprocessableEntity(ApiResponseDto<WhatsAppDestinatarioDto>.Fail(
                $"Máximo {WhatsAppConfig.MaxDestinatariosActivos} destinatarios activos."));

        var accion = activo ? "WHATSAPP_DESTINATARIO_EDITAR" : "WHATSAPP_DESTINATARIO_DESACTIVAR";
        if (telefono != r.Telefono) r.Verificado = await VerificarAsync(telefono, ct);
        r.Nombre        = nombre;
        r.Telefono      = telefono;
        r.Activo        = activo;
        r.ActualizadoEn = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        await _audit.RegistrarAsync(accion, "WhatsAppRecipient", r.Id.ToString(), UsuarioId(),
            new { r.Nombre, telefono = WhatsAppConstantes.Enmascarar(r.Telefono), r.Activo });
        return Ok(ApiResponseDto<WhatsAppDestinatarioDto>.Ok(MapDestinatario(r, false), MensajeVerificacion(r)));
    }

    // ── Versión de WhatsApp Web ──────────────────────────────────

    [HttpGet("version-sugerida")]
    public async Task<IActionResult> VersionSugerida(CancellationToken ct)
    {
        var v = await _versiones.ObtenerSugeridaAsync(ct);
        return Ok(ApiResponseDto<WhatsAppVersionSugeridaDto>.Ok(new WhatsAppVersionSugeridaDto { Version = v },
            v is null ? "No se pudo detectar; escríbela a mano." : ""));
    }

    [HttpPost("reiniciar")]
    public async Task<IActionResult> Reiniciar(CancellationToken ct)
    {
        if (!_config.Configurado)
            return StatusCode(503, ApiResponseDto<object>.Fail("WhatsApp no está configurado en el servidor."));

        var ultimo = _estadoActual.UltimoReinicioEn;
        if (ultimo is not null && DateTime.UtcNow - ultimo.Value < WhatsAppConfig.EsperaEntreReinicios)
            return Conflict(ApiResponseDto<object>.Fail("Espera un minuto antes de volver a reconectar."));

        var r = await _evolution.ReiniciarInstanciaAsync(ct);
        if (!r.Exito)
        {
            if (r.Inalcanzable)
                return NoDisponible<object>("El servicio de WhatsApp no responde; si acaba de despertar, intenta en un minuto.");
            return StatusCode(502, ApiResponseDto<object>.Fail("WhatsApp rechazó la reconexión; inténtalo de nuevo más tarde."));
        }

        _estadoActual.UltimoReinicioEn = DateTime.UtcNow;

        // Estado real tras el reinicio; si no se puede consultar, el monitor y el webhook lo actualizarán.
        var estado = await _evolution.ObtenerEstadoAsync(ct);
        var actual = estado.Exito
            ? WhatsAppConnectionMonitor.Traducir(estado.Valor)
            : (await ObtenerSettingsAsync()).EstadoConexion;
        if (estado.Exito) await _monitor.RegistrarEstadoAsync(actual, "reinicio");

        await _audit.RegistrarAsync("WHATSAPP_REINICIO", "WhatsAppSettings", "1", UsuarioId());
        return Ok(ApiResponseDto<object>.Ok(new { estado = actual }, "Reconexión solicitada."));
    }

    // ── Historial ────────────────────────────────────────────────

    [HttpGet("mensajes")]
    public async Task<IActionResult> Mensajes([FromQuery] int limite = 50)
    {
        if (limite < 1 || limite > 200)
            return BadRequest(ApiResponseDto<List<WhatsAppMensajeDto>>.Fail("El límite debe estar entre 1 y 200."));

        var lista = await _context.WhatsAppMessages.AsNoTracking()
            .OrderByDescending(m => m.CreadoEn)
            .Take(limite)
            .Select(m => new { m.CreadoEn, m.EnviadoEn, m.TipoEvento, m.Recipient.Nombre, m.Recipient.Telefono,
                               m.Estado, m.Intentos, m.UltimoError })
            .ToListAsync();

        return Ok(ApiResponseDto<List<WhatsAppMensajeDto>>.Ok(lista.Select(m => new WhatsAppMensajeDto
        {
            CreadoEn     = m.CreadoEn,
            EnviadoEn    = m.EnviadoEn,
            TipoEvento   = m.TipoEvento,
            Destinatario = $"{m.Nombre} ({WhatsAppConstantes.Enmascarar(m.Telefono)})",
            Estado       = m.Estado,
            Intentos     = m.Intentos,
            UltimoError  = m.UltimoError
        }).ToList()));
    }

    // ── Helpers ──────────────────────────────────────────────────

    private async Task<WhatsAppSettings> ObtenerSettingsAsync()
    {
        var s = await _context.WhatsAppSettings.FirstOrDefaultAsync(x => x.Id == 1);
        if (s is null)
        {
            s = new WhatsAppSettings { Id = 1 };
            _context.WhatsAppSettings.Add(s);
            await _context.SaveChangesAsync();
        }
        return s;
    }

    private async Task<bool?> VerificarAsync(string telefono, CancellationToken ct)
    {
        // Requiere estar conectado; si no se puede, se guarda sin verificar (no bloquea el alta).
        var r = await _evolution.VerificarNumeroAsync(telefono, ct);
        return r.Exito ? r.Valor : null;
    }

    private static string MensajeVerificacion(WhatsAppRecipient r) => r.Verificado switch
    {
        false => "Guardado, pero ese número no parece tener WhatsApp.",
        null  => "Guardado. No se pudo verificar el número (WhatsApp no está conectado).",
        _     => "Destinatario guardado."
    };

    private static (string nombre, string telefono, string? error) Validar(WhatsAppDestinatarioRequestDto dto)
    {
        var nombre   = (dto.Nombre ?? "").Trim();
        var telefono = new string((dto.Telefono ?? "").Where(char.IsDigit).ToArray());
        if (nombre.Length is < 1 or > 100) return (nombre, telefono, "El nombre debe tener entre 1 y 100 caracteres.");
        if (!WhatsAppConstantes.RegexTelefono.IsMatch(telefono))
            return (nombre, telefono, "Teléfono inválido: solo dígitos, 12 o 13, empezando con 52.");
        return (nombre, telefono, null);
    }

    private Guid? UsuarioId()
    {
        var claim = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private ObjectResult NoDisponible<T>(string? mensaje) =>
        StatusCode(503, ApiResponseDto<T>.Fail(mensaje ?? "El servicio de WhatsApp no está disponible."));

    private WhatsAppEstadoDto MapEstado(WhatsAppSettings s) => new()
    {
        Configurado          = _config.Configurado,
        Habilitado           = s.Habilitado,
        Estado               = _config.Configurado ? s.EstadoConexion : WhatsAppConstantes.NoConfigurado,
        NumeroVinculado      = s.NumeroVinculado,
        UltimaVerificacionEn = s.UltimaVerificacionEn,
        DesconectadoDesde    = s.DesconectadoDesde
    };

    private static WhatsAppConfiguracionDto MapConfiguracion(WhatsAppSettings s) => new()
    {
        Habilitado                = s.Habilitado,
        NotificarPedidoNuevo      = s.NotificarPedidoNuevo,
        NotificarCambioEstado     = s.NotificarCambioEstado,
        EstadosNotificados        = s.EstadosNotificados.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
        NotificarStockBajo        = s.NotificarStockBajo,
        NotificarVentaInstantanea = s.NotificarVentaInstantanea,
        NotificarReabastecimiento = s.NotificarReabastecimiento
    };

    private static WhatsAppDestinatarioDto MapDestinatario(WhatsAppRecipient r, bool conTelefono) => new()
    {
        Id                  = r.Id,
        Nombre              = r.Nombre,
        TelefonoEnmascarado = WhatsAppConstantes.Enmascarar(r.Telefono),
        Telefono            = conTelefono ? r.Telefono : null,
        Activo              = r.Activo,
        Verificado          = r.Verificado
    };
}
