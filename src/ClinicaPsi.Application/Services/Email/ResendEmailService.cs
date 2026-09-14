using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClinicaPsi.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClinicaPsi.Application.Services.Email;

public class ResendEmailService : IEmailService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<EmailOptions> _options;
    private readonly IConfiguration _configuration;
    private readonly ConfiguracaoService _configuracaoService;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<EmailOptions> options,
        IConfiguration configuration,
        ConfiguracaoService configuracaoService,
        ILogger<ResendEmailService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _configuration = configuration;
        _configuracaoService = configuracaoService;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ResolveApiKey());

    public string EffectiveFrom
    {
        get
        {
            var opts = _options.CurrentValue;
            var from = ResolveFromAddress(opts);
            var name = ResolveFromName(opts);
            return string.IsNullOrWhiteSpace(name) ? from : $"{name} <{from}>";
        }
    }

    public async Task<EmailSendResult> SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
            return EmailSendResult.Fail("Destinatário vazio.");

        var apiKey = ResolveApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("E-mail não enviado para {To}: RESEND_API_KEY não configurada", toEmail);
            return EmailSendResult.Fail("RESEND_API_KEY não configurada.");
        }

        var opts = _options.CurrentValue;
        var fromAddress = await ResolveFromAddressAsync(opts);
        var fromName = await ResolveFromNameAsync(opts);
        var fromHeader = string.IsNullOrWhiteSpace(fromName) ? fromAddress : $"{fromName} <{fromAddress}>";

        try
        {
            var httpClient = _httpClientFactory.CreateClient("Resend");
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = JsonContent.Create(new ResendPayload
            {
                From = fromHeader,
                To = new[] { toEmail.Trim() },
                Subject = subject,
                Html = htmlBody
            });

            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Resend falhou ({Status}) ao enviar para {To}: {Body}. From={From}",
                    (int)response.StatusCode, toEmail, body, fromHeader);
                return EmailSendResult.Fail($"Resend {(int)response.StatusCode}: {TrimForUi(body)}");
            }

            string? id = null;
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("id", out var idProp))
                    id = idProp.GetString();
            }
            catch { }

            _logger.LogInformation("E-mail enviado via Resend para {To}. Id={Id} From={From}", toEmail, id, fromHeader);
            return EmailSendResult.Ok(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar e-mail via Resend para {To}", toEmail);
            return EmailSendResult.Fail(ex.Message);
        }
    }

    private string? ResolveApiKey()
    {
        var opts = _options.CurrentValue.ApiKey;
        if (!string.IsNullOrWhiteSpace(opts)) return opts.Trim();
        return FirstNonEmpty(_configuration["RESEND_API_KEY"], _configuration["Email:ApiKey"], _configuration["Email__ApiKey"]);
    }

    private string ResolveFromAddress(EmailOptions opts) =>
        FirstNonEmpty(opts.From, _configuration["Email:From"], _configuration["Email__From"], "noreply@psiianasantos.com.br")!;

    private string ResolveFromName(EmailOptions opts) =>
        FirstNonEmpty(opts.FromName, _configuration["Email:FromName"], _configuration["Email__FromName"], "Psicóloga Ana Santos")!;

    private async Task<string> ResolveFromAddressAsync(EmailOptions opts)
    {
        var fromDb = await _configuracaoService.ObterValorStringAsync("Email.From");
        return FirstNonEmpty(fromDb, ResolveFromAddress(opts))!;
    }

    private async Task<string> ResolveFromNameAsync(EmailOptions opts)
    {
        var fromDb = await _configuracaoService.ObterValorStringAsync("Email.FromName");
        return FirstNonEmpty(fromDb, ResolveFromName(opts))!;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
            if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
        return null;
    }

    private static string TrimForUi(string body)
    {
        if (string.IsNullOrEmpty(body)) return "sem detalhes";
        return body.Length <= 240 ? body : body[..240] + "…";
    }

    private sealed class ResendPayload
    {
        [JsonPropertyName("from")] public string From { get; set; } = string.Empty;
        [JsonPropertyName("to")] public string[] To { get; set; } = Array.Empty<string>();
        [JsonPropertyName("subject")] public string Subject { get; set; } = string.Empty;
        [JsonPropertyName("html")] public string Html { get; set; } = string.Empty;
    }
}
