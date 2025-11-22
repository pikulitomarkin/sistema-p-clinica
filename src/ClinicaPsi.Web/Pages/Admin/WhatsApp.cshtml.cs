using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ClinicaPsi.Application.Services;

namespace ClinicaPsi.Web.Pages.Admin
{
    [Authorize(Policy = "AdminPolicy")]
    public class WhatsAppModel : PageModel
    {
        private readonly ConfiguracaoService _configuracaoService;
        private readonly IConfiguration _configuration;

        public WhatsAppModel(ConfiguracaoService configuracaoService, IConfiguration configuration)
        {
            _configuracaoService = configuracaoService;
            _configuration = configuration;
        }

        [BindProperty]
        public string? PhoneNumberId { get; set; }

        [BindProperty]
        public string? AccessToken { get; set; }

        [BindProperty]
        public string? VerifyToken { get; set; }

        [BindProperty]
        public string? AppSecret { get; set; }

        [BindProperty]
        public string? OpenAIApiKey { get; set; }

        public bool IsConfigurado { get; set; }
        public string? WebhookUrl { get; set; }
        public string? MensagemSucesso { get; set; }
        public string? MensagemErro { get; set; }

        public async Task OnGetAsync()
        {
            await CarregarConfiguracoesAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                // Salvar configurações do WhatsApp
                if (!string.IsNullOrEmpty(PhoneNumberId))
                    await _configuracaoService.SalvarAsync("WhatsApp:PhoneNumberId", PhoneNumberId);

                if (!string.IsNullOrEmpty(AccessToken))
                    await _configuracaoService.SalvarAsync("WhatsApp:AccessToken", AccessToken);

                if (!string.IsNullOrEmpty(VerifyToken))
                    await _configuracaoService.SalvarAsync("WhatsApp:VerifyToken", VerifyToken);

                if (!string.IsNullOrEmpty(AppSecret))
                    await _configuracaoService.SalvarAsync("WhatsApp:AppSecret", AppSecret);

                if (!string.IsNullOrEmpty(OpenAIApiKey))
                    await _configuracaoService.SalvarAsync("OpenAI:ApiKey", OpenAIApiKey);

                MensagemSucesso = "Configurações salvas com sucesso!";
            }
            catch (Exception ex)
            {
                MensagemErro = $"Erro ao salvar configurações: {ex.Message}";
            }

            await CarregarConfiguracoesAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostTestarAsync()
        {
            try
            {
                await CarregarConfiguracoesAsync();

                if (!IsConfigurado)
                {
                    MensagemErro = "Configure o WhatsApp antes de testar a conexão.";
                    return Page();
                }

                // Teste simples: verificar se as configurações estão preenchidas
                var phoneId = await _configuracaoService.ObterValorStringAsync("WhatsApp:PhoneNumberId");
                var token = await _configuracaoService.ObterValorStringAsync("WhatsApp:AccessToken");

                if (string.IsNullOrEmpty(phoneId) || string.IsNullOrEmpty(token))
                {
                    MensagemErro = "Configurações incompletas. Verifique Phone Number ID e Access Token.";
                    return Page();
                }

                // Aqui você pode adicionar uma chamada real à API do WhatsApp para testar
                // Por exemplo: GET https://graph.facebook.com/v18.0/{phone-number-id}
                
                MensagemSucesso = "Configurações parecem corretas! Teste enviando uma mensagem para o número do WhatsApp Business.";
            }
            catch (Exception ex)
            {
                MensagemErro = $"Erro ao testar conexão: {ex.Message}";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostLimparAsync()
        {
            try
            {
                await _configuracaoService.RemoverAsync("WhatsApp:PhoneNumberId");
                await _configuracaoService.RemoverAsync("WhatsApp:AccessToken");
                await _configuracaoService.RemoverAsync("WhatsApp:VerifyToken");
                await _configuracaoService.RemoverAsync("WhatsApp:AppSecret");
                await _configuracaoService.RemoverAsync("OpenAI:ApiKey");

                MensagemSucesso = "Configurações removidas com sucesso!";
            }
            catch (Exception ex)
            {
                MensagemErro = $"Erro ao limpar configurações: {ex.Message}";
            }

            await CarregarConfiguracoesAsync();
            return Page();
        }

        private async Task CarregarConfiguracoesAsync()
        {
            PhoneNumberId = await _configuracaoService.ObterValorStringAsync("WhatsApp:PhoneNumberId") ?? "";
            AccessToken = await _configuracaoService.ObterValorStringAsync("WhatsApp:AccessToken") ?? "";
            VerifyToken = await _configuracaoService.ObterValorStringAsync("WhatsApp:VerifyToken") ?? "";
            AppSecret = await _configuracaoService.ObterValorStringAsync("WhatsApp:AppSecret") ?? "";
            OpenAIApiKey = await _configuracaoService.ObterValorStringAsync("OpenAI:ApiKey") ?? "";

            IsConfigurado = !string.IsNullOrEmpty(PhoneNumberId) && !string.IsNullOrEmpty(AccessToken);

            // Obter URL base do site
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            WebhookUrl = $"{baseUrl}/api/whatsapp/webhook";
        }
    }
}
