using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FloreriaBautista.Data;
using FloreriaBautista.Services.WhatsApp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Controllers;

/// <summary>
/// Ruta interna de la integración de WhatsApp: el webhook de Evolution.
/// Es pública porque el backend es un Web Service público: la defensa es el token, comparado en
/// tiempo constante. Nunca se persisten payloads.
/// </summary>
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/internal/whatsapp")]
[AllowAnonymous]
public class WhatsAppInternalController : ControllerBase
{
    private readonly AppDbContext                         _context;
    private readonly WhatsAppConnectionMonitor            _monitor;
    private readonly WhatsAppConfig                       _config;
    private readonly ILogger<WhatsAppInternalController>  _logger;

    public WhatsAppInternalController(AppDbContext context, WhatsAppConnectionMonitor monitor,
        WhatsAppConfig config, ILogger<WhatsAppInternalController> logger)
    {
        _context = context;
        _monitor = monitor;
        _config  = config;
        _logger  = logger;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook([FromQuery] string? token)
    {
        // El token se valida antes de leer el cuerpo: sin token → 401 aunque el cuerpo sea inválido.
        var recibido = Request.Headers["x-webhook-token"].FirstOrDefault() ?? token;
        if (!_config.Configurado || !TokenValido(recibido, _config.WebhookToken)) return Unauthorized();

        JsonElement payload;
        try
        {
            using var doc = await JsonDocument.ParseAsync(Request.Body);
            payload = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return Ok();
        }

        // Solo se usa `event` y `data.state`; el resto del payload se ignora.
        var evento = payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("event", out var e)
            && e.ValueKind == JsonValueKind.String ? e.GetString() : null;
        if (!string.Equals(evento?.Replace('_', '.'), "connection.update", StringComparison.OrdinalIgnoreCase))
            return Ok();

        string? state = null;
        if (payload.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("state", out var st) && st.ValueKind == JsonValueKind.String)
            state = st.GetString();
        if (state is null) return Ok();

        await _monitor.RegistrarEstadoAsync(WhatsAppConnectionMonitor.Traducir(state), "webhook");
        return Ok();
    }

    private static bool TokenValido(string? recibido, string? esperado)
    {
        if (string.IsNullOrEmpty(recibido) || string.IsNullOrEmpty(esperado)) return false;
        var a = SHA256.HashData(Encoding.UTF8.GetBytes(recibido));
        var b = SHA256.HashData(Encoding.UTF8.GetBytes(esperado));
        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}
