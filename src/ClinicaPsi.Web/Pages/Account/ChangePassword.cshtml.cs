using System.ComponentModel.DataAnnotations;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClinicaPsi.Web.Pages.Account;

[Authorize]
public class ChangePasswordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<ChangePasswordModel> _logger;
    public ChangePasswordModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, ILogger<ChangePasswordModel> logger)
    { _userManager = userManager; _signInManager = signInManager; _logger = logger; }
    [BindProperty] public InputModel Input { get; set; } = new();
    public bool Obrigatorio { get; set; }
    public class InputModel
    {
        [Required, DataType(DataType.Password)] public string SenhaAtual { get; set; } = string.Empty;
        [Required, StringLength(100, MinimumLength = 6), DataType(DataType.Password)] public string NovaSenha { get; set; } = string.Empty;
        [DataType(DataType.Password), Compare(nameof(NovaSenha))] public string ConfirmarSenha { get; set; } = string.Empty;
    }
    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToPage("./Login");
        Obrigatorio = user.MustChangePassword; return Page();
    }
    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToPage("./Login");
        Obrigatorio = user.MustChangePassword;
        if (!ModelState.IsValid) return Page();
        var result = await _userManager.ChangePasswordAsync(user, Input.SenhaAtual, Input.NovaSenha);
        if (!result.Succeeded) { foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description); return Page(); }
        user.MustChangePassword = false; await _userManager.UpdateAsync(user); await _signInManager.RefreshSignInAsync(user);
        _logger.LogInformation("Usuário {Email} alterou a senha", user.Email);
        return user.TipoUsuario switch
        {
            TipoUsuario.Admin => RedirectToPage("/Admin/Index"),
            TipoUsuario.Psicologo => RedirectToPage("/Psicologo/Index"),
            TipoUsuario.Cliente => RedirectToPage("/Cliente/Index"),
            _ => RedirectToPage("/Index")
        };
    }
}
