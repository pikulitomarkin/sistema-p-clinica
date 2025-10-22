using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ClinicaPsi.Shared.Models;

namespace ClinicaPsi.Web.Pages.Test
{
    public class LoginPsicologoModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public LoginPsicologoModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        public void OnGet()
        {
            ViewData["Message"] = "Clique no botão para fazer login automático como psicólogo";
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Fazer logout se já estiver logado
            if (User.Identity?.IsAuthenticated == true)
            {
                await _signInManager.SignOutAsync();
            }

            // Encontrar o usuário psicólogo
            var user = await _userManager.FindByEmailAsync("joao.silva@psii.com");
            if (user == null)
            {
                ViewData["Message"] = "Usuário psicólogo não encontrado!";
                return Page();
            }

            // Verificar roles
            var roles = await _userManager.GetRolesAsync(user);
            ViewData["Message"] = $"Usuário encontrado. Roles: {string.Join(", ", roles)}. TipoUsuario: {user.TipoUsuario}. PsicologoId: {user.PsicologoId}";

            // Fazer login
            await _signInManager.SignInAsync(user, isPersistent: false);

            return RedirectToPage("/Psicologo/Index");
        }
    }
}