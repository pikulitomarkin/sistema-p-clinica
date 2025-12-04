using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Diagnostics;

namespace ClinicaPsi.Application.Services;

public class WhatsAppWebService
{
    private static readonly ActivitySource ActivitySource = new("ClinicaPsi.WhatsApp");
    
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
        using var activity = ActivitySource.StartActivity("WhatsApp.GerarQRCode");
        activity?.SetTag("whatsapp.session", sessionName);
        
        try
        {
            _logger.LogInformation("Gerando QR Code para sessão: {SessionName}", sessionName);

            var response = await _httpClient.GetAsync($"{_venomBotUrl}/qrcode?sessionName={sessionName}");
            activity?.SetTag("http.status_code", (int)response.StatusCode);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<VenomBotResponse>(content);

                var session = await ObterSessaoAsync(sessionName);
                
                // Verificar se conectou durante o processo (QR Code foi escaneado)
                if (result?.Connected == true)
                {
                    _logger.LogInformation("✅ WhatsApp conectado com sucesso durante geração do QR Code!");
                    session.Status = "Conectado";
                    session.LastConnection = DateTime.UtcNow;
                    session.UpdatedAt = DateTime.UtcNow;
                    session.QRCode = null; // Limpar QR Code pois já conectou
                    await _context.SaveChangesAsync();
                    return session;
                }
                
                // IMPORTANTE: Retornar o QR Code diretamente da API, não do banco
                // O servidor Node.js limpa o QR Code do banco após salvar
                
                // Atualizar apenas status e expiry no banco
                session.QRCodeExpiry = DateTime.UtcNow.AddMinutes(2);
                session.Status = "QRCode";
                session.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Mas retornar o QR Code da resposta da API
                session.QRCode = result?.QrCode;
                
                _logger.LogInformation("QR Code recebido da API: {Length} caracteres", session.QRCode?.Length ?? 0);

                return session;
            }

            // Se retornou 400, pode ser "já conectado" ou "conectando"
            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Bot retornou erro 400: {Error}", errorContent);
                
                // Se já está conectado, tentar desconectar e gerar novo QR
                if (errorContent.Contains("conectado") || errorContent.Contains("CONNECTED"))
                {
                    _logger.LogInformation("Cliente reportado como conectado, tentando desconectar...");
                    await DesconectarAsync(sessionName);
                    
                    // Aguardar 2 segundos
                    await Task.Delay(2000);
                    
                    // Tentar gerar QR Code novamente
                    _logger.LogInformation("Tentando gerar QR Code novamente após desconexão...");
                    response = await _httpClient.GetAsync($"{_venomBotUrl}/qrcode?sessionName={sessionName}");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var result = JsonSerializer.Deserialize<VenomBotResponse>(content);
                        
                        // Verificar se conectou automaticamente (sessão persistida no volume)
                        if (result?.Connected == true)
                        {
                            _logger.LogInformation("Bot reconectou automaticamente usando sessão persistida no volume");
                            var session = await ObterSessaoAsync(sessionName);
                            session.Status = "Conectado";
                            session.LastConnection = DateTime.UtcNow;
                            session.QRCode = null;
                            session.UpdatedAt = DateTime.UtcNow;
                            await _context.SaveChangesAsync();
                            return session;
                        }
                        
                        var sessionResult = await ObterSessaoAsync(sessionName);
                        
                        sessionResult.QRCodeExpiry = DateTime.UtcNow.AddMinutes(2);
                        sessionResult.Status = "QRCode";
                        sessionResult.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                        
                        sessionResult.QRCode = result?.QrCode;
                        _logger.LogInformation("QR Code gerado após desconexão: {Length} caracteres", sessionResult.QRCode?.Length ?? 0);
                        return sessionResult;
                    }
                }
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
            _logger.LogInformation("Obtendo status da sessão: {SessionName}", sessionName);
            
            var response = await _httpClient.GetAsync($"{_venomBotUrl}/status?session={sessionName}");
            
            _logger.LogInformation("Status Code da API: {StatusCode}", response.StatusCode);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Resposta da API: {Content}", content);
                
                var result = JsonSerializer.Deserialize<VenomBotStatusResponse>(content);

                var session = await ObterSessaoAsync(sessionName);
                
                _logger.LogInformation("Status retornado da API - Connected: {Connected}, Phone: {Phone}", 
                    result?.Connected, result?.PhoneNumber);
                
                if (result?.Connected == true)
                {
                    session.Status = "Conectado";
                    session.PhoneNumber = result.PhoneNumber;
                    session.LastConnection = DateTime.UtcNow;
                    _logger.LogInformation("Status atualizado para Conectado");
                }
                else
                {
                    session.Status = "Desconectado";
                    _logger.LogInformation("Status atualizado para Desconectado");
                }
                
                session.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return new WhatsAppStatusInfo
                {
                    Conectado = result?.Connected ?? false,
                    NumeroTelefone = result?.PhoneNumber,
                    UltimaConexao = session.LastConnection,
                    Status = session.Status,
                    SessionName = session.SessionName
                };
            }

            _logger.LogWarning("Falha ao obter status: {StatusCode}", response.StatusCode);
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
    
    [System.Text.Json.Serialization.JsonPropertyName("connected")]
    public bool? Connected { get; set; }
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
