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
    private readonly ILogger<ForgotPasswordModel> _logger;

    public ForgotPasswordModel(UserManager<ApplicationUser> userManager, UsuarioPacienteOnboardingService onboardingService, ILogger<ForgotPasswordModel> logger)
    {
        _userManager = userManager; _onboardingService = onboardingService; _logger = logger;
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
        var user = await _userManager.FindByEmailAsync(Input.Email.Trim());
        if (user == null) { _logger.LogInformation("ForgotPassword: e-mail não encontrado ({Email})", Input.Email); return Page(); }
        try
        {
            var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(await _userManager.GeneratePasswordResetTokenAsync(user)));
            var resetUrl = $"{_onboardingService.ResolvePublicBaseUrl()}/Account/ResetPassword?code={UrlEncoder.Default.Encode(code)}&email={UrlEncoder.Default.Encode(user.Email!)}";
            var result = await _onboardingService.EnviarResetSenhaAsync(user, resetUrl);
            if (!result.Success) _logger.LogWarning("Falha ao enviar reset de senha para {Email}: {Erro}", user.Email, result.ErrorMessage);
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro no fluxo ForgotPassword para {Email}", Input.Email); }
        return Page();
    }
}
