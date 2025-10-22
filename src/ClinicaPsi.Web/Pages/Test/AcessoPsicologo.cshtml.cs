using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ClinicaPsi.Shared.Models;

namespace ClinicaPsi.Web.Pages.Test
{
    public class AcessoPsicologoModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public AcessoPsicologoModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public ApplicationUser? CurrentUser { get; set; }

        public async Task OnGetAsync()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                CurrentUser = await _userManager.GetUserAsync(User);
            }
        }

        public async Task<IActionResult> OnPostLoginAsync()
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
                TempData["Error"] = "Usuário psicólogo não encontrado!";
                return RedirectToPage();
            }

            // Fazer login
            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToPage();
        }
    }
}