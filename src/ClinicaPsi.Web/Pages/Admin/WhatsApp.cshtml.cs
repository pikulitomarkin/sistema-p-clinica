using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ClinicaPsi.Application.Services;

namespace ClinicaPsi.Web.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class WhatsAppModel : PageModel
    {
        private readonly WhatsAppWebService _whatsAppService;
        private readonly ILogger<WhatsAppModel> _logger;

        public WhatsAppModel(
            WhatsAppWebService whatsAppService,
            ILogger<WhatsAppModel> logger)
        {
            _whatsAppService = whatsAppService;
            _logger = logger;
        }

        public WhatsAppStatusInfo? StatusInfo { get; set; }
        public string? QRCodeBase64 { get; set; }
        public DateTime? QRCodeExpira { get; set; }
        public string? Mensagem { get; set; }
        public bool Sucesso { get; set; }

        public async Task OnGetAsync()
        {
            try
            {
                // Obter status da conexão
                StatusInfo = await _whatsAppService.ObterStatusAsync("default");
                
                _logger.LogInformation($"Status WhatsApp: {StatusInfo?.Status ?? "null"}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar página WhatsApp");
                Mensagem = $"Erro ao conectar com o serviço WhatsApp: {ex.Message}";
                Sucesso = false;
            }
        }

        public async Task<IActionResult> OnPostAtualizarStatusAsync()
        {
            try
            {
                StatusInfo = await _whatsAppService.ObterStatusAsync("default");
                
                Mensagem = "Status atualizado com sucesso!";
                Sucesso = true;
                
                _logger.LogInformation("Status WhatsApp atualizado");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar status");
                Mensagem = $"Erro ao atualizar status: {ex.Message}";
                Sucesso = false;
            }

            return Page();
        }

        public async Task<IActionResult> OnPostGerarQRCodeAsync()
        {
            try
            {
                var sessao = await _whatsAppService.GerarQRCodeAsync("default");
                
                if (sessao != null && !string.IsNullOrEmpty(sessao.QRCode))
                {
                    QRCodeBase64 = sessao.QRCode;
                    QRCodeExpira = sessao.QRCodeExpiry;
                    StatusInfo = new WhatsAppStatusInfo
                    {
                        Conectado = false,
                        NumeroTelefone = sessao.PhoneNumber,
                        SessionName = sessao.SessionName,
                        UltimaConexao = sessao.LastConnection
                    };
                    
                    Mensagem = "QR Code gerado! Escaneie com seu WhatsApp em até 2 minutos.";
                    Sucesso = true;
                    
                    _logger.LogInformation("QR Code gerado com sucesso");
                }
                else
                {
                    Mensagem = "Não foi possível gerar o QR Code. Tente novamente.";
                    Sucesso = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar QR Code");
                Mensagem = $"Erro ao gerar QR Code: {ex.Message}";
                Sucesso = false;
            }

            return Page();
        }

        public async Task<IActionResult> OnPostDesconectarAsync()
        {
            try
            {
                var resultado = await _whatsAppService.DesconectarAsync("default");
                
                if (resultado)
                {
                    Mensagem = "WhatsApp desconectado com sucesso!";
                    Sucesso = true;
                    
                    _logger.LogInformation("WhatsApp desconectado");
                }
                else
                {
                    Mensagem = "Não foi possível desconectar. Tente novamente.";
                    Sucesso = false;
                }

                // Atualizar status após desconectar
                StatusInfo = await _whatsAppService.ObterStatusAsync("default");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao desconectar WhatsApp");
                Mensagem = $"Erro ao desconectar: {ex.Message}";
                Sucesso = false;
            }

            return Page();
        }

        public async Task<IActionResult> OnPostEnviarTesteAsync(string numeroTeste, string mensagemTeste)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(numeroTeste) || string.IsNullOrWhiteSpace(mensagemTeste))
                {
                    Mensagem = "Preencha o número e a mensagem!";
                    Sucesso = false;
                    return Page();
                }

                // Limpar número (remover espaços, traços, etc)
                numeroTeste = new string(numeroTeste.Where(char.IsDigit).ToArray());

                var resultado = await _whatsAppService.EnviarMensagemAsync(
                    numeroTeste,
                    mensagemTeste,
                    "default"
                );

                if (resultado)
                {
                    Mensagem = $"Mensagem enviada com sucesso para {numeroTeste}!";
                    Sucesso = true;
                    
                    _logger.LogInformation($"Mensagem teste enviada para {numeroTeste}");
                }
                else
                {
                    Mensagem = "Não foi possível enviar a mensagem. Verifique se o WhatsApp está conectado.";
                    Sucesso = false;
                }

                // Atualizar status
                StatusInfo = await _whatsAppService.ObterStatusAsync("default");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar mensagem teste");
                Mensagem = $"Erro ao enviar mensagem: {ex.Message}";
                Sucesso = false;
            }

            return Page();
        }
    }
}
