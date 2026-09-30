using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using ClinicaPsi.Shared.Models;

namespace ClinicaPsi.Web.Pages.Account;

public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(SignInManager<ApplicationUser> signInManager, ILogger<LoginModel> logger)
    {
        _signInManager = signInManager;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "O email é obrigatório")]
        [EmailAddress(ErrorMessage = "Email inválido")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "A senha é obrigatória")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Lembrar de mim")]
        public bool RememberMe { get; set; }
    }

    public async Task OnGetAsync(string? returnUrl = null)
    {
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ModelState.AddModelError(string.Empty, ErrorMessage);
        }

        // Clear the existing external cookie to ensure a clean login process
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;

        if (ModelState.IsValid)
        {
            var result = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                _logger.LogInformation("Usuário logado com sucesso.");

                var user = await _signInManager.UserManager.FindByEmailAsync(Input.Email)
                    ?? await _signInManager.UserManager.FindByNameAsync(Input.Email);

                if (user != null)
                {
                    if (user.MustChangePassword)
                        return RedirectToPage("./ChangePassword");

                    return LocalRedirect(await ResolvePostLoginPathAsync(user, returnUrl));
                }

                return LocalRedirect(GetHomeForTipo(TipoUsuario.Cliente));
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("Conta do usuário bloqueada.");
                return RedirectToPage("./Lockout");
            }

            ModelState.AddModelError(string.Empty, "Tentativa de login inválida.");
            return Page();
        }

        return Page();
    }

    /// <summary>
    /// Destino pós-login: ReturnUrl seguro/apropriado quando houver; senão landing por papel.
    /// Paciente → Minha Área (/cliente); Psicólogo → /psicologo; Admin → /admin.
    /// </summary>
    private async Task<string> ResolvePostLoginPathAsync(ApplicationUser user, string? returnUrl)
    {
        var tipo = await ResolveTipoUsuarioAsync(user);
        var home = GetHomeForTipo(tipo);

        if (!IsMeaningfulReturnUrl(returnUrl))
            return home;

        if (!IsReturnUrlAllowedForTipo(returnUrl!, tipo))
            return home;

        return returnUrl!;
    }

    private async Task<TipoUsuario> ResolveTipoUsuarioAsync(ApplicationUser user)
    {
        if (user.TipoUsuario is TipoUsuario.Admin or TipoUsuario.Psicologo or TipoUsuario.Cliente)
            return user.TipoUsuario;

        // Fallback por roles Identity (usuários antigos / TipoUsuario não preenchido)
        if (await _signInManager.UserManager.IsInRoleAsync(user, "Admin"))
            return TipoUsuario.Admin;
        if (await _signInManager.UserManager.IsInRoleAsync(user, "Psicologo"))
            return TipoUsuario.Psicologo;
        if (await _signInManager.UserManager.IsInRoleAsync(user, "Cliente"))
            return TipoUsuario.Cliente;

        return TipoUsuario.Cliente;
    }

    private static string GetHomeForTipo(TipoUsuario tipo) => tipo switch
    {
        TipoUsuario.Admin => "/admin",
        TipoUsuario.Psicologo => "/psicologo",
        _ => "/cliente" // Minha Área
    };

    /// <summary>
    /// ReturnUrl só é usado se for local, não for a própria tela de login/logout,
    /// e não for só a home pública (nesse caso preferimos a área do papel).
    /// </summary>
    private bool IsMeaningfulReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return false;

        if (!Url.IsLocalUrl(returnUrl))
            return false;

        var path = returnUrl.Split('?', '#')[0].TrimEnd('/');
        if (string.IsNullOrEmpty(path) || path == "~" || path == "/" ||
            path.Equals("~/", StringComparison.OrdinalIgnoreCase))
            return false;

        if (path.Equals("/Account/Login", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/Account/Logout", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/Account/Register", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/Account/AccessDenied", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    /// <summary>
    /// Evita mandar paciente para área admin/psico (e vice-versa) via ReturnUrl de challenge.
    /// </summary>
    private static bool IsReturnUrlAllowedForTipo(string returnUrl, TipoUsuario tipo)
    {
        var path = returnUrl.Split('?', '#')[0];

        return tipo switch
        {
            TipoUsuario.Cliente =>
                !StartsWithIgnoreCase(path, "/admin") &&
                !StartsWithIgnoreCase(path, "/psicologo") &&
                !StartsWithIgnoreCase(path, "/Prontuario"),

            TipoUsuario.Psicologo =>
                !StartsWithIgnoreCase(path, "/admin") &&
                !StartsWithIgnoreCase(path, "/cliente"),

            TipoUsuario.Admin => true,

            _ => false
        };
    }

    private static bool StartsWithIgnoreCase(string path, string prefix) =>
        path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
}
