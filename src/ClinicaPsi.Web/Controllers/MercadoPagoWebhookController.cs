using System.Text.Json;
using ClinicaPsi.Application.Services.MercadoPago;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ClinicaPsi.Web.Controllers;

/// <summary>
/// Receptor de notificações Mercado Pago.
/// URL pública: https://psyall.com.br/webhook
/// Alias: /api/mercadopago/webhook
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

        if (!MercadoPagoSignatureValidator.TryValidate(
                _options.WebhookSecret,
                xSignature,
                xRequestId,
                paymentDataId,
                out var sigError))
        {
            _logger.LogWarning(
                "Webhook MP rejeitado ({Error}). path={Path} hasSignature={HasSig} hasRequestId={HasRid} dataId={DataId}",
                sigError, Request.Path, !string.IsNullOrEmpty(xSignature), !string.IsNullOrEmpty(xRequestId), paymentDataId);

            return Unauthorized(new
            {
                success = false,
                error = sigError ?? "unauthorized"
            });
        }

        try
        {
            await _mp.HandleWebhookAsync(topic ?? type, id, paymentDataId, ct);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar webhook Mercado Pago");
            return StatusCode(500, new { success = false });
        }
    }

    /// <summary>Probe: rota existe; sem assinatura → 401.</summary>
    [HttpGet("/webhook")]
    [HttpGet("/api/mercadopago/webhook")]
    public IActionResult Probe()
    {
        var configured = !string.IsNullOrWhiteSpace(_options.WebhookSecret);
        return Unauthorized(new
        {
            success = false,
            error = configured ? "x_signature_missing" : "webhook_secret_missing",
            hint = "POST com headers x-signature e x-request-id"
        });
    }
}
