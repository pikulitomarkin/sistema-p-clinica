using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ClinicaPsi.Application.Services;

public class WhatsAppWebService
{
    private readonly AppDbContext _context;
    private readonly ILogger<WhatsAppWebService> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _venomBotUrl;

    public WhatsAppWebService(
        AppDbContext context,
        ILogger<WhatsAppWebService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        
        // URL do container Venom-Bot no Railway (será configurada depois)
        _venomBotUrl = Environment.GetEnvironmentVariable("VENOM_BOT_URL") ?? "http://localhost:3000";
    }

    public async Task<WhatsAppSession> ObterSessaoAsync(string sessionName = "default")
    {
        var session = await _context.WhatsAppSessions
            .FirstOrDefaultAsync(s => s.SessionName == sessionName);

        if (session == null)
        {
            session = new WhatsAppSession
            {
                SessionName = sessionName,
                Status = "Desconectado",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.WhatsAppSessions.Add(session);
            await _context.SaveChangesAsync();
        }

        return session;
    }

    public async Task<WhatsAppSession?> GerarQRCodeAsync(string sessionName = "default")
    {
        try
        {
            _logger.LogInformation("Gerando QR Code para sessão: {SessionName}", sessionName);

            var response = await _httpClient.GetAsync($"{_venomBotUrl}/qrcode?sessionName={sessionName}");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<VenomBotResponse>(content);

                var session = await ObterSessaoAsync(sessionName);
                session.QRCode = result?.QrCode;
                session.QRCodeExpiry = DateTime.UtcNow.AddMinutes(2);
                session.Status = "QRCode";
                session.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return session;
            }

            _logger.LogWarning("Falha ao gerar QR Code: {StatusCode}", response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar QR Code");
            return null;
        }
    }

    public async Task<bool> EnviarMensagemAsync(string numeroDestino, string mensagem, string sessionName = "default")
    {
        try
        {
            _logger.LogInformation("Enviando mensagem para {Numero}", numeroDestino);

            var payload = new
            {
                session = sessionName,
                number = numeroDestino,
                message = mensagem
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync($"{_venomBotUrl}/send", content);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar mensagem");
            return false;
        }
    }

    public async Task<WhatsAppStatusInfo> ObterStatusAsync(string sessionName = "default")
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_venomBotUrl}/status?session={sessionName}");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<VenomBotStatusResponse>(content);

                var session = await ObterSessaoAsync(sessionName);
                
                if (result?.Connected == true)
                {
                    session.Status = "Conectado";
                    session.PhoneNumber = result.PhoneNumber;
                    session.LastConnection = DateTime.UtcNow;
                }
                else
                {
                    session.Status = "Desconectado";
                }
                
                session.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return new WhatsAppStatusInfo
                {
                    Conectado = result?.Connected ?? false,
                    NumeroTelefone = result?.PhoneNumber,
                    UltimaConexao = session.LastConnection,
                    Status = session.Status
                };
            }

            return new WhatsAppStatusInfo { Conectado = false, Status = "Erro" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter status");
            return new WhatsAppStatusInfo { Conectado = false, Status = "Erro" };
        }
    }

    public async Task<bool> DesconectarAsync(string sessionName = "default")
    {
        try
        {
            var response = await _httpClient.PostAsync($"{_venomBotUrl}/disconnect?session={sessionName}", null);
            
            if (response.IsSuccessStatusCode)
            {
                var session = await ObterSessaoAsync(sessionName);
                session.Status = "Desconectado";
                session.QRCode = null;
                session.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao desconectar");
            return false;
        }
    }
}

// DTOs
public class VenomBotResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("qrCode")]
    public string? QrCode { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("message")]
    public string? Message { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("expired")]
    public bool? Expired { get; set; }
}

public class VenomBotStatusResponse
{
    public bool Connected { get; set; }
    public string? PhoneNumber { get; set; }
}

public class WhatsAppStatusInfo
{
    public bool Conectado { get; set; }
    public string? NumeroTelefone { get; set; }
    public DateTime? UltimaConexao { get; set; }
    public string SessionName { get; set; } = "default";
    public string Status { get; set; } = "Desconectado";
}
