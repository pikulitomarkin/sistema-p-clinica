using Microsoft.AspNetCore.Mvc;
using ClinicaPsi.Application.Services;

namespace ClinicaPsi.Web.Controllers;

[ApiController]
[Route("webhook")]
public class WhatsAppWebhookController : ControllerBase
{
    private readonly WhatsAppNotificationService _notificationService;
    private readonly ILogger<WhatsAppWebhookController> _logger;

    public WhatsAppWebhookController(
        WhatsAppNotificationService notificationService,
        ILogger<WhatsAppWebhookController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    [HttpPost("whatsapp")]
    public async Task<IActionResult> ReceberMensagem([FromBody] WhatsAppWebhookPayload payload)
    {
        try
        {
            _logger.LogInformation("📨 Webhook recebido de {From}: {Message}", 
                payload.From, payload.Message);

            // Processar mensagem de forma assíncrona (não bloquear resposta)
            _ = Task.Run(async () =>
            {
                try
                {
                    await _notificationService.ProcessarMensagemRecebida(
                        payload.From, 
                        payload.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao processar mensagem do webhook");
                }
            });

            return Ok(new { success = true, message = "Mensagem recebida" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no webhook WhatsApp");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }
}

public class WhatsAppWebhookPayload
{
    public string SessionName { get; set; } = "default";
    public string From { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime Timestamp { get; set; }
}
