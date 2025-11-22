using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ClinicaPsi.Application.Services;

/// <summary>
/// Serviço de integração com WhatsApp Business API (Meta/Facebook)
/// Documentação: https://developers.facebook.com/docs/whatsapp/cloud-api
/// </summary>
public class WhatsAppService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WhatsAppService> _logger;
    private readonly string _accessToken;
    private readonly string _phoneNumberId;
    private readonly string _businessAccountId;
    private const string BaseUrl = "https://graph.facebook.com/v18.0";

    public WhatsAppService(ILogger<WhatsAppService> logger, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("WhatsApp");
        
        // Configurações devem estar no appsettings.json
        _accessToken = configuration["WhatsApp:AccessToken"] ?? throw new InvalidOperationException("WhatsApp Access Token não configurado");
        _phoneNumberId = configuration["WhatsApp:PhoneNumberId"] ?? throw new InvalidOperationException("WhatsApp Phone Number ID não configurado");
        _businessAccountId = configuration["WhatsApp:BusinessAccountId"] ?? "";
        
        _httpClient.BaseAddress = new Uri(BaseUrl);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
    }

    /// <summary>
    /// Envia mensagem de texto simples
    /// </summary>
    public async Task<WhatsAppResponse> EnviarMensagemTextoAsync(string numeroDestino, string mensagem)
    {
        try
        {
            var payload = new
            {
                messaging_product = "whatsapp",
                to = LimparNumeroTelefone(numeroDestino),
                type = "text",
                text = new { body = mensagem }
            };

            var response = await EnviarRequisicaoAsync($"/{_phoneNumberId}/messages", payload);
            _logger.LogInformation($"Mensagem WhatsApp enviada para {numeroDestino}");
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Erro ao enviar mensagem WhatsApp para {numeroDestino}");
            throw;
        }
    }

    /// <summary>
    /// Envia mensagem usando template pré-aprovado
    /// </summary>
    public async Task<WhatsAppResponse> EnviarMensagemTemplateAsync(
        string numeroDestino,
        string nomeTemplate,
        string idioma = "pt_BR",
        params string[] parametros)
    {
        try
        {
            var components = new List<object>();
            
            if (parametros?.Length > 0)
            {
                components.Add(new
                {
                    type = "body",
                    parameters = parametros.Select(p => new
                    {
                        type = "text",
                        text = p
                    }).ToArray()
                });
            }

            var payload = new
            {
                messaging_product = "whatsapp",
                to = LimparNumeroTelefone(numeroDestino),
                type = "template",
                template = new
                {
                    name = nomeTemplate,
                    language = new { code = idioma },
                    components = components.ToArray()
                }
            };

            var response = await EnviarRequisicaoAsync($"/{_phoneNumberId}/messages", payload);
            _logger.LogInformation($"Template WhatsApp '{nomeTemplate}' enviado para {numeroDestino}");
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Erro ao enviar template WhatsApp para {numeroDestino}");
            throw;
        }
    }

    /// <summary>
    /// Envia lembrete de consulta (usando template)
    /// </summary>
    public async Task<WhatsAppResponse> EnviarLembreteConsultaAsync(
        string numeroDestino,
        string nomePaciente,
        string nomePsicologo,
        DateTime dataHorario)
    {
        var dataFormatada = dataHorario.ToString("dd/MM/yyyy");
        var horaFormatada = dataHorario.ToString("HH:mm");
        
        // Template deve estar pré-aprovado no Facebook Business Manager
        return await EnviarMensagemTemplateAsync(
            numeroDestino,
            "lembrete_consulta", // Nome do template
            "pt_BR",
            nomePaciente,
            dataFormatada,
            horaFormatada,
            nomePsicologo
        );
    }

    /// <summary>
    /// Envia confirmação de agendamento
    /// </summary>
    public async Task<WhatsAppResponse> EnviarConfirmacaoAgendamentoAsync(
        string numeroDestino,
        string nomePaciente,
        DateTime dataHorario,
        string endereco)
    {
        var dataFormatada = dataHorario.ToString("dd/MM/yyyy 'às' HH:mm");
        
        return await EnviarMensagemTemplateAsync(
            numeroDestino,
            "confirmacao_agendamento",
            "pt_BR",
            nomePaciente,
            dataFormatada,
            endereco
        );
    }

    /// <summary>
    /// Envia mensagem de cancelamento
    /// </summary>
    public async Task<WhatsAppResponse> EnviarCancelamentoAsync(
        string numeroDestino,
        string nomePaciente,
        DateTime dataHorario,
        string motivo = "")
    {
        var mensagem = $"Olá {nomePaciente},\n\n" +
                      $"Sua consulta marcada para {dataHorario:dd/MM/yyyy 'às' HH:mm} foi cancelada.\n\n";
        
        if (!string.IsNullOrEmpty(motivo))
            mensagem += $"Motivo: {motivo}\n\n";
        
        mensagem += "Entre em contato para reagendar.";
        
        return await EnviarMensagemTextoAsync(numeroDestino, mensagem);
    }

    /// <summary>
    /// Envia mensagem de boas-vindas
    /// </summary>
    public async Task<WhatsAppResponse> EnviarBoasVindasAsync(string numeroDestino, string nomePaciente)
    {
        var mensagem = $"Olá {nomePaciente}! 👋\n\n" +
                      $"Bem-vindo(a) à Clínica Psii Ana Santos!\n\n" +
                      $"Estamos muito felizes em tê-lo(a) conosco. " +
                      $"Em breve entraremos em contato para agendar sua primeira consulta.\n\n" +
                      $"Qualquer dúvida, estamos à disposição!";
        
        return await EnviarMensagemTextoAsync(numeroDestino, mensagem);
    }

    /// <summary>
    /// Método auxiliar para enviar requisições HTTP
    /// </summary>
    private async Task<WhatsAppResponse> EnviarRequisicaoAsync(string endpoint, object payload)
    {
        var jsonContent = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(endpoint, content);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError($"Erro WhatsApp API: {response.StatusCode} - {responseContent}");
            throw new HttpRequestException($"WhatsApp API Error: {response.StatusCode}");
        }

        var whatsAppResponse = JsonSerializer.Deserialize<WhatsAppResponse>(responseContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return whatsAppResponse ?? new WhatsAppResponse();
    }

    /// <summary>
    /// Remove caracteres especiais do número de telefone
    /// Formato esperado: 5511999999999 (código país + DDD + número)
    /// </summary>
    private string LimparNumeroTelefone(string numero)
    {
        var numeroLimpo = new string(numero.Where(char.IsDigit).ToArray());
        
        // Garantir formato brasileiro (55 + DDD + número)
        if (!numeroLimpo.StartsWith("55") && numeroLimpo.Length >= 10)
            numeroLimpo = "55" + numeroLimpo;
        
        return numeroLimpo;
    }

    /// <summary>
    /// Verifica se o serviço está configurado corretamente
    /// </summary>
    public bool EstaConfigurado()
    {
        return !string.IsNullOrEmpty(_accessToken) && !string.IsNullOrEmpty(_phoneNumberId);
    }
}

/// <summary>
/// Resposta da API do WhatsApp
/// </summary>
public class WhatsAppResponse
{
    public string? MessagingProduct { get; set; }
    public List<WhatsAppContact>? Contacts { get; set; }
    public List<WhatsAppMessage>? Messages { get; set; }
    public WhatsAppError? Error { get; set; }
    public bool Sucesso => Error == null;
}

public class WhatsAppContact
{
    public string? Input { get; set; }
    public string? WaId { get; set; }
}

public class WhatsAppMessage
{
    public string? Id { get; set; }
    public string? MessageStatus { get; set; }
}

public class WhatsAppError
{
    public int Code { get; set; }
    public string? Message { get; set; }
    public string? Type { get; set; }
    public string? FbTraceId { get; set; }
}
