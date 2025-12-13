using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace ClinicaPsi.Web.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public RegisterModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required(ErrorMessage = "Nome é obrigatório")]
            [Display(Name = "Nome Completo")]
            public string Nome { get; set; } = string.Empty;

            [Required(ErrorMessage = "Email é obrigatório")]
            [EmailAddress(ErrorMessage = "Email inválido")]
            [Display(Name = "Email")]
            public string Email { get; set; } = string.Empty;

            [Phone(ErrorMessage = "Telefone inválido")]
            [Display(Name = "Telefone")]
            public string? PhoneNumber { get; set; }

            [Required(ErrorMessage = "Senha é obrigatória")]
            [StringLength(100, ErrorMessage = "{0} deve ter entre {2} e {1} caracteres.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Senha")]
            public string Password { get; set; } = string.Empty;

            [DataType(DataType.Password)]
            [Display(Name = "Confirmar Senha")]
            [Compare("Password", ErrorMessage = "As senhas não coincidem")]
            public string ConfirmPassword { get; set; } = string.Empty;
        }

        public async Task OnGetAsync()
        {
            // Se o usuário já está logado, redirecionar para a home
            if (User.Identity?.IsAuthenticated == true)
            {
                RedirectToPage("/");
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var user = new ApplicationUser
                    {
                        UserName = Input?.Email ?? string.Empty,
                        Email = Input?.Email ?? string.Empty,
                        PhoneNumber = Input?.PhoneNumber,
                        NomeCompleto = Input?.Nome ?? string.Empty,
                        DataCadastro = DateTime.Now,
                        Ativo = true,
                        TipoUsuario = TipoUsuario.Paciente
                    };

                    var result = await _userManager.CreateAsync(user, Input?.Password ?? string.Empty);

                    if (result.Succeeded)
                    {
                        // Fazer login automático
                        await _signInManager.SignInAsync(user, isPersistent: false);

                        // Redirecionar para página de agendamento
                        return RedirectToPage("/Agendamento");
                    }

                    // Se houver erros, adicionar à ModelState
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, $"Erro ao criar conta: {ex.Message}");
                }
            }

            return Page();
        }
    }
}
