using System.ComponentModel.DataAnnotations;
using System.Text;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace ClinicaPsi.Web.Pages.Account;

[AllowAnonymous]
public class ResetPasswordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    public ResetPasswordModel(UserManager<ApplicationUser> userManager) => _userManager = userManager;
    [BindProperty] public InputModel Input { get; set; } = new();
    public class InputModel
    {
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
        [Required, StringLength(100, MinimumLength = 6), DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
        [DataType(DataType.Password), Compare(nameof(Password))] public string ConfirmPassword { get; set; } = string.Empty;
        [Required] public string Code { get; set; } = string.Empty;
    }
    public IActionResult OnGet(string? code = null, string? email = null)
    {
        if (string.IsNullOrWhiteSpace(code)) return BadRequest("Código de redefinição inválido.");
        Input = new InputModel { Code = code, Email = email ?? string.Empty };
        return Page();
    }
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var user = await _userManager.FindByEmailAsync(Input.Email.Trim());
        if (user == null) return RedirectToPage("./ResetPasswordConfirmation");
        string decoded;
        try { decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(Input.Code)); }
        catch { ModelState.AddModelError(string.Empty, "Link de redefinição inválido ou expirado."); return Page(); }
        var result = await _userManager.ResetPasswordAsync(user, decoded, Input.Password);
        if (result.Succeeded) { user.MustChangePassword = false; await _userManager.UpdateAsync(user); return RedirectToPage("./ResetPasswordConfirmation"); }
        foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
        return Page();
    }
}
