using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ClinicaPsi.Application.Services;

public class OpenAIService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<OpenAIService> _logger;

    public OpenAIService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<OpenAIService> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _apiKey = configuration["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI API key not configured");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        _logger = logger;
    }

    public async Task<string> GetChatResponseAsync(string prompt, string systemPrompt = "Você é um assistente útil em português.")
    {
        try
        {
            var payload = new
            {
                model = "gpt-4o-mini",
                messages = new[] {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = prompt }
                },
                max_tokens = 500,
                temperature = 0.2
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("OpenAI error: {Status} {Body}", resp.StatusCode, body);
                return "Desculpe, não consegui processar sua solicitação agora.";
            }

            using var doc = JsonDocument.Parse(body);
            var choices = doc.RootElement.GetProperty("choices");
            var firstChoice = choices.EnumerateArray().FirstOrDefault();
            if (firstChoice.ValueKind == JsonValueKind.Undefined) return "";
            var message = firstChoice.GetProperty("message");
            var text = message.GetProperty("content").GetString();
            return text ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao chamar OpenAI");
            return "Desculpe, ocorreu um erro interno ao processar sua solicitação.";
        }
    }
}
