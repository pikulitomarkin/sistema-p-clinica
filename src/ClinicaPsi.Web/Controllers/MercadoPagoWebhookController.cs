using System.Text.Json;
using ClinicaPsi.Application.Services.MercadoPago;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ClinicaPsi.Web.Controllers;

/// <summary>
/// Receptor de notificações Mercado Pago.
/// URL pública: https://psyall.com.br/webhook (alias: /api/mercadopago/webhook).
/// </summary>
[ApiController]
[AllowAnonymous]
[IgnoreAntiforgeryToken]
public class MercadoPagoWebhookController : ControllerBase
{
    private readonly MercadoPagoService _mp;
    private readonly MercadoPagoOptions _options;
    private readonly ILogger<MercadoPagoWebhookController> _logger;

    public MercadoPagoWebhookController(
        MercadoPagoService mp,
        IOptions<MercadoPagoOptions> options,
        ILogger<MercadoPagoWebhookController> logger)
    {
        _mp = mp;
        _options = options.Value;
        _logger = logger;
    }

    [HttpPost("/webhook")]
    [HttpPost("/api/mercadopago/webhook")]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        var xSignature = Request.Headers["x-signature"].FirstOrDefault()
            ?? Request.Headers["X-Signature"].FirstOrDefault();
        var xRequestId = Request.Headers["x-request-id"].FirstOrDefault()
            ?? Request.Headers["X-Request-Id"].FirstOrDefault();

        var dataId = Request.Query["data.id"].FirstOrDefault()
            ?? Request.Query["id"].FirstOrDefault();
        var topic = Request.Query["topic"].FirstOrDefault()
            ?? Request.Query["type"].FirstOrDefault();
        var type = Request.Query["type"].FirstOrDefault();
        string? id = Request.Query["id"].FirstOrDefault();

        if (Request.ContentLength is > 0 ||
            Request.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
        {
            try
            {
                using var doc = await JsonDocument.ParseAsync(Request.Body, cancellationToken: ct);
                var root = doc.RootElement;
                if (root.TryGetProperty("topic", out var t))
                    topic ??= t.GetString();
                if (root.TryGetProperty("type", out var ty))
                {
                    type ??= ty.GetString();
                    topic ??= type;
                }
                if (root.TryGetProperty("action", out var a) && string.IsNullOrEmpty(topic))
                    topic = a.GetString();
                if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
                    && data.TryGetProperty("id", out var idEl))
                {
                    dataId ??= idEl.ValueKind == JsonValueKind.Number
                        ? idEl.GetRawText()
                        : idEl.GetString();
                }
                else if (root.TryGetProperty("id", out var idRoot))
                {
                    id ??= idRoot.ValueKind == JsonValueKind.Number
                        ? idRoot.GetRawText()
                        : idRoot.GetString();
                    dataId ??= id;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogDebug(ex, "Body JSON do webhook MP inválido ou vazio — seguindo com query/headers");
            }
        }

        var paymentDataId = dataId ?? id;

        // Só valida assinatura se WebhookSecret estiver configurado
        if (!string.IsNullOrWhiteSpace(_options.WebhookSecret))
        {
            if (!MercadoPagoSignatureValidator.TryValidate(
                    _options.WebhookSecret,
                    xSignature,
                    xRequestId,
                    paymentDataId,
                    out var sigError))
            {
                _logger.LogWarning(
                    "Webhook MP rejeitado ({Error}). path={Path} dataId={DataId}",
                    sigError, Request.Path, paymentDataId);
                return Unauthorized(new { success = false, error = sigError ?? "unauthorized" });
            }
        }

        try
        {
            await _mp.HandleWebhookAsync(topic ?? type, id, paymentDataId, ct);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar webhook Mercado Pago");
            // 200 evita storm de retries em erros transitórios; log já registra
            return Ok(new { success = true, warning = true });
        }
    }

    [HttpGet("/webhook")]
    [HttpGet("/api/mercadopago/webhook")]
    public IActionResult Probe()
    {
        return Ok(new
        {
            service = "mercadopago-webhook",
            configured = _mp.IsConfigured,
            signatureRequired = !string.IsNullOrWhiteSpace(_options.WebhookSecret),
            sandbox = _options.UseSandbox
        });
    }
}
