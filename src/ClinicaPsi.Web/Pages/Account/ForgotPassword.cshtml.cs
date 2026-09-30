using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using ClinicaPsi.Application.Services;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace ClinicaPsi.Web.Pages.Account;

[AllowAnonymous]
public class ForgotPasswordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly UsuarioPacienteOnboardingService _onboardingService;
    private readonly PacienteService _pacienteService;
    private readonly ILogger<ForgotPasswordModel> _logger;

    public ForgotPasswordModel(
        UserManager<ApplicationUser> userManager,
        UsuarioPacienteOnboardingService onboardingService,
        PacienteService pacienteService,
        ILogger<ForgotPasswordModel> logger)
    {
        _userManager = userManager;
        _onboardingService = onboardingService;
        _pacienteService = pacienteService;
        _logger = logger;
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    public bool EmailEnviado { get; set; }
    public class InputModel
    {
        [Required(ErrorMessage = "Informe o e-mail")]
        [EmailAddress(ErrorMessage = "E-mail inválido")]
        public string Email { get; set; } = string.Empty;
    }
    public void OnGet() { }
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        EmailEnviado = true;
        var email = Input.Email.Trim();
        var user = await _userManager.FindByEmailAsync(email);

        // Paciente cadastrado sem AspNetUser: provisiona conta e envia reset
        // (antes o fluxo só logava "não encontrado" e a UI fingia sucesso — e-mail nunca saía).
        if (user == null)
        {
            var paciente = await _pacienteService.GetByEmailAsync(email);
            if (paciente != null)
            {
                var (provisioned, err) = await _onboardingService.GarantirUsuarioClienteSemEmailAsync(paciente);
                if (provisioned == null)
                    _logger.LogWarning("ForgotPassword: paciente {Id} sem usuário e falha ao provisionar: {Erro}", paciente.Id, err);
                else
                    user = provisioned;
            }
            else
            {
                _logger.LogInformation("ForgotPassword: e-mail não encontrado ({Email})", email);
                return Page();
            }
        }

        if (user == null) return Page();

        try
        {
            var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(await _userManager.GeneratePasswordResetTokenAsync(user)));
            var resetUrl = $"{_onboardingService.ResolvePublicBaseUrl()}/Account/ResetPassword?code={UrlEncoder.Default.Encode(code)}&email={UrlEncoder.Default.Encode(user.Email!)}";
            var result = await _onboardingService.EnviarResetSenhaAsync(user, resetUrl);
            if (!result.Success)
                _logger.LogWarning("Falha ao enviar reset de senha para {Email}: {Erro}", user.Email, result.ErrorMessage);
            else
                _logger.LogInformation("ForgotPassword: reset enviado para {Email}", user.Email);
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro no fluxo ForgotPassword para {Email}", email); }
        return Page();
    }
}
