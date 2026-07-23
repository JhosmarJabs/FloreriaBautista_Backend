using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FloreriaBautista.Models.DTOs.Payments;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Services.Interfaces;

namespace FloreriaBautista.Services.Payments;

// Integración con Mercado Pago (Checkout Pro) vía API REST directa.
// No usa el SDK de NuGet para evitar dependencias extra en el contenedor.
public class MercadoPagoService : IMercadoPagoService
{
    private const string ApiBase   = "https://api.mercadopago.com";
    private const string Currency  = "MXN";

    private readonly HttpClient _http;
    private readonly ILogger<MercadoPagoService> _logger;

    public MercadoPagoService(HttpClient http, ILogger<MercadoPagoService> logger)
    {
        _http   = http;
        _logger = logger;
    }

    private static string? AccessToken => Environment.GetEnvironmentVariable("MERCADOPAGO_ACCESS_TOKEN");
    private static string  FrontendUrl => (Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "http://localhost:3001").TrimEnd('/');
    private static string? BackendPublicUrl => Environment.GetEnvironmentVariable("BACKEND_PUBLIC_URL")?.TrimEnd('/');

    public bool EstaConfigurado => !string.IsNullOrWhiteSpace(AccessToken);

    private HttpRequestMessage NuevaPeticion(HttpMethod metodo, string ruta)
    {
        var token = AccessToken
            ?? throw new AppException("Mercado Pago no está configurado (falta MERCADOPAGO_ACCESS_TOKEN).");
        var req = new HttpRequestMessage(metodo, $"{ApiBase}{ruta}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return req;
    }

    public async Task<PreferenceResponseDto> CrearPreferenciaAsync(
        Guid orderId, string descripcion, List<MpPreferenceItem> items, string? payerEmail)
    {
        var frontend = FrontendUrl;

        var body = new Dictionary<string, object?>
        {
            ["items"] = items.Select(i => new Dictionary<string, object?>
            {
                ["title"]       = i.Title,
                ["quantity"]    = i.Quantity,
                ["unit_price"]  = i.UnitPrice,
                ["currency_id"] = Currency,
            }).ToList(),
            ["external_reference"] = orderId.ToString(),
            ["statement_descriptor"] = "FLORERIA BAUTISTA",
            ["back_urls"] = new Dictionary<string, object?>
            {
                ["success"] = $"{frontend}/checkout/exito",
                ["failure"] = $"{frontend}/checkout/fallo",
                ["pending"] = $"{frontend}/checkout/pendiente",
            },
        };

        // auto_return regresa al sitio automáticamente al aprobar, pero Mercado Pago
        // EXIGE una URL pública https en back_urls.success. En localhost lo rechaza
        // ("auto_return invalid"), así que solo se activa con https (ngrok / producción).
        if (frontend.StartsWith("https://"))
            body["auto_return"] = "approved";

        // Webhook solo si hay una URL pública del backend (ngrok / dominio real).
        if (!string.IsNullOrWhiteSpace(BackendPublicUrl))
            body["notification_url"] = $"{BackendPublicUrl}/api/payments/mercadopago/webhook";

        if (!string.IsNullOrWhiteSpace(payerEmail))
            body["payer"] = new Dictionary<string, object?> { ["email"] = payerEmail };

        var req = NuevaPeticion(HttpMethod.Post, "/checkout/preferences");
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var res  = await _http.SendAsync(req);
        var json = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
        {
            _logger.LogError("Mercado Pago rechazó la preferencia [{Status}]: {Body}", res.StatusCode, json);
            throw new AppException("No se pudo iniciar el pago con Mercado Pago. Intenta de nuevo.");
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var id        = root.GetProperty("id").GetString() ?? "";
        var initPoint = root.TryGetProperty("init_point", out var ip) ? ip.GetString() ?? "" : "";

        _logger.LogInformation("Preferencia MP creada {PreferenceId} para orden {OrderId}", id, orderId);
        return new PreferenceResponseDto { PreferenceId = id, InitPoint = initPoint };
    }

    public async Task<MpPaymentInfo?> ConsultarPagoAsync(string paymentId)
    {
        if (string.IsNullOrWhiteSpace(paymentId)) return null;

        var req = NuevaPeticion(HttpMethod.Get, $"/v1/payments/{paymentId}");
        var res = await _http.SendAsync(req);
        var json = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
        {
            _logger.LogWarning("No se pudo consultar el pago {PaymentId} [{Status}]", paymentId, res.StatusCode);
            return null;
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var status = root.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "";
        var extRef = root.TryGetProperty("external_reference", out var er) ? er.GetString() : null;
        decimal amount = root.TryGetProperty("transaction_amount", out var ta) && ta.ValueKind == JsonValueKind.Number
            ? ta.GetDecimal() : 0m;

        return new MpPaymentInfo(paymentId, status, extRef, amount);
    }

    public async Task<MpPaymentInfo?> BuscarPagoAprobadoPorOrdenAsync(Guid orderId)
    {
        var req = NuevaPeticion(HttpMethod.Get, $"/v1/payments/search?external_reference={orderId}&sort=date_created&criteria=desc");
        var res = await _http.SendAsync(req);
        var json = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
        {
            _logger.LogWarning("Búsqueda de pagos falló para orden {OrderId} [{Status}]", orderId, res.StatusCode);
            return null;
        }

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            return null;

        // Se prioriza un pago aprobado; si no hay, se devuelve el más reciente (para reflejar pendiente/rechazado).
        MpPaymentInfo? primero = null;
        foreach (var p in results.EnumerateArray())
        {
            var id     = p.TryGetProperty("id", out var idEl) ? idEl.ToString() : "";
            var status = p.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "";
            var extRef = p.TryGetProperty("external_reference", out var er) ? er.GetString() : orderId.ToString();
            decimal amount = p.TryGetProperty("transaction_amount", out var ta) && ta.ValueKind == JsonValueKind.Number
                ? ta.GetDecimal() : 0m;
            var info = new MpPaymentInfo(id, status, extRef, amount);

            if (status == "approved") return info;
            primero ??= info;
        }
        return primero;
    }
}
