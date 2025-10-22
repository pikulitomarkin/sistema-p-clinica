using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using ClinicaPsi.Web.Extensions;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

namespace ClinicaPsi.Web.Pages.Cliente
{
    [Authorize]
    public class MeuPerfilModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public MeuPerfilModel(AppDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public Paciente? Paciente { get; set; }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        [BindProperty]
        public SenhaModel InputSenha { get; set; } = new();

        public class InputModel
        {
            [Required(ErrorMessage = "Nome é obrigatório")]
            [StringLength(100, ErrorMessage = "Nome deve ter no máximo 100 caracteres")]
            public string Nome { get; set; } = string.Empty;

            [Required(ErrorMessage = "Email é obrigatório")]
            [EmailAddress(ErrorMessage = "Email inválido")]
            public string Email { get; set; } = string.Empty;

            [Phone(ErrorMessage = "Telefone inválido")]
            public string? Telefone { get; set; }

            [Required(ErrorMessage = "CPF é obrigatório")]
            [StringLength(11, MinimumLength = 11, ErrorMessage = "CPF deve ter 11 dígitos")]
            public string CPF { get; set; } = string.Empty;

            [Required(ErrorMessage = "Data de nascimento é obrigatória")]
            public DateTime DataNascimento { get; set; }

            [StringLength(200, ErrorMessage = "Endereço deve ter no máximo 200 caracteres")]
            public string? Endereco { get; set; }

            [StringLength(100, ErrorMessage = "Contato de emergência deve ter no máximo 100 caracteres")]
            public string? ContatoEmergencia { get; set; }

            [Phone(ErrorMessage = "Telefone de emergência inválido")]
            public string? TelefoneEmergencia { get; set; }

            public string? HistoricoMedico { get; set; }
            public string? MedicamentosUso { get; set; }
            public string? Observacoes { get; set; }
        }

        public class SenhaModel
        {
            [Required(ErrorMessage = "Senha atual é obrigatória")]
            [DataType(DataType.Password)]
            public string SenhaAtual { get; set; } = string.Empty;

            [Required(ErrorMessage = "Nova senha é obrigatória")]
            [StringLength(100, ErrorMessage = "A senha deve ter pelo menos {2} e no máximo {1} caracteres.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            public string NovaSenha { get; set; } = string.Empty;

            [DataType(DataType.Password)]
            [Compare("NovaSenha", ErrorMessage = "A nova senha e a confirmação não coincidem.")]
            public string ConfirmarSenha { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            await CarregarDadosAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAtualizarPerfilAsync()
        {
            if (!ModelState.IsValid)
            {
                await CarregarDadosAsync();
                return Page();
            }

            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user?.PacienteId == null)
                {
                    ModelState.AddModelError("", "Usuário não está associado a um paciente.");
                    await CarregarDadosAsync();
                    return Page();
                }

                var paciente = await _context.Pacientes.FindAsync(user.PacienteId.Value);
                if (paciente == null)
                {
                    ModelState.AddModelError("", "Paciente não encontrado.");
                    await CarregarDadosAsync();
                    return Page();
                }

                // Verificar se CPF já existe para outro paciente
                var cpfExistente = await _context.Pacientes
                    .Where(p => p.CPF == Input.CPF && p.Id != paciente.Id)
                    .FirstOrDefaultAsync();

                if (cpfExistente != null)
                {
                    ModelState.AddModelError("Input.CPF", "CPF já está cadastrado para outro paciente.");
                    await CarregarDadosAsync();
                    return Page();
                }

                // Verificar se email já existe para outro usuário
                var emailExistente = await _userManager.FindByEmailAsync(Input.Email);
                if (emailExistente != null && emailExistente.Id != userId)
                {
                    ModelState.AddModelError("Input.Email", "Email já está sendo usado por outro usuário.");
                    await CarregarDadosAsync();
                    return Page();
                }

                // Atualizar dados do paciente
                paciente.Nome = Input.Nome;
                paciente.Email = Input.Email;
                paciente.Telefone = Input.Telefone;
                paciente.CPF = Input.CPF;
                paciente.DataNascimento = Input.DataNascimento;
                paciente.Endereco = Input.Endereco;
                paciente.ContatoEmergencia = Input.ContatoEmergencia;
                paciente.TelefoneEmergencia = Input.TelefoneEmergencia;
                paciente.HistoricoMedico = Input.HistoricoMedico;
                paciente.MedicamentosUso = Input.MedicamentosUso;
                paciente.Observacoes = Input.Observacoes;
                paciente.DataAtualizacao = DateTime.Now;

                // Atualizar email do usuário Identity
                var identityUser = await _userManager.FindByIdAsync(userId!);
                if (identityUser != null && identityUser.Email != Input.Email)
                {
                    identityUser.Email = Input.Email;
                    identityUser.UserName = Input.Email;
                    identityUser.NormalizedEmail = Input.Email.ToUpper();
                    identityUser.NormalizedUserName = Input.Email.ToUpper();
                    
                    await _userManager.UpdateAsync(identityUser);
                }

                _context.Pacientes.Update(paciente);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Perfil atualizado com sucesso!";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Erro ao atualizar perfil: " + ex.Message);
                await CarregarDadosAsync();
                return Page();
            }
        }

        public async Task<IActionResult> OnPostAlterarSenhaAsync()
        {
            // Limpar erros do modelo de perfil para validar apenas senha
            ModelState.Clear();
            
            if (!TryValidateModel(InputSenha, nameof(InputSenha)))
            {
                await CarregarDadosAsync();
                return Page();
            }

            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var user = await _userManager.FindByIdAsync(userId!);

                if (user == null)
                {
                    ModelState.AddModelError("", "Usuário não encontrado.");
                    await CarregarDadosAsync();
                    return Page();
                }

                var result = await _userManager.ChangePasswordAsync(user, InputSenha.SenhaAtual, InputSenha.NovaSenha);

                if (result.Succeeded)
                {
                    TempData["Success"] = "Senha alterada com sucesso!";
                    
                    // Limpar campos de senha
                    InputSenha = new SenhaModel();
                    
                    return RedirectToPage();
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }
                }

                await CarregarDadosAsync();
                return Page();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Erro ao alterar senha: " + ex.Message);
                await CarregarDadosAsync();
                return Page();
            }
        }

        private async Task CarregarDadosAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user?.PacienteId != null)
            {
                Paciente = await _context.Pacientes
                    .FindAsync(user.PacienteId.Value);

                if (Paciente != null)
                {
                    Input.Nome = Paciente.Nome;
                    Input.Email = Paciente.Email;
                    Input.Telefone = Paciente.Telefone;
                    Input.CPF = Paciente.CPF;
                    Input.DataNascimento = Paciente.DataNascimento;
                    Input.Endereco = Paciente.Endereco;
                    Input.ContatoEmergencia = Paciente.ContatoEmergencia;
                    Input.TelefoneEmergencia = Paciente.TelefoneEmergencia;
                    Input.HistoricoMedico = Paciente.HistoricoMedico;
                    Input.MedicamentosUso = Paciente.MedicamentosUso;
                    Input.Observacoes = Paciente.Observacoes;
                }
            }
        }

        public int CalcularIdade()
        {
            if (Paciente == null) return 0;
            
            var hoje = DateTime.Today;
            var idade = hoje.Year - Paciente.DataNascimento.Year;
            
            if (Paciente.DataNascimento.Date > hoje.AddYears(-idade))
                idade--;
                
            return idade;
        }
    }
}