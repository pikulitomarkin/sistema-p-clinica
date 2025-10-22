using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using ClinicaPsi.Application.Services;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace ClinicaPsi.Web.Pages.Admin.Usuarios
{
    [Authorize]
    public class NovoModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AuditoriaService _auditoriaService;

        public NovoModel(
            AppDbContext context, 
            UserManager<ApplicationUser> userManager, 
            RoleManager<IdentityRole> roleManager,
            AuditoriaService auditoriaService)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _auditoriaService = auditoriaService;
        }

        [BindProperty]
        public NovoUsuarioInputModel Input { get; set; } = new();

        public List<ClinicaPsi.Shared.Models.Psicologo> PsicologosDisponiveis { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !await _userManager.IsInRoleAsync(user, "Admin"))
            {
                return Forbid();
            }

            await CarregarPsicologosDisponiveisAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !await _userManager.IsInRoleAsync(user, "Admin"))
            {
                return Forbid();
            }

            await CarregarPsicologosDisponiveisAsync();

            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Validações específicas
            if (Input.TipoUsuario == TipoUsuario.Psicologo && !Input.PsicologoId.HasValue)
            {
                ModelState.AddModelError("Input.PsicologoId", "Selecione um psicólogo para associar ao usuário.");
                return Page();
            }

            if (Input.TipoUsuario == TipoUsuario.Cliente && !string.IsNullOrWhiteSpace(Input.CPF))
            {
                // Verificar se já existe paciente com este CPF
                var pacienteExistente = await _context.Pacientes.FirstOrDefaultAsync(p => p.CPF == Input.CPF);
                if (pacienteExistente != null)
                {
                    ModelState.AddModelError("Input.CPF", "Já existe um paciente cadastrado com este CPF.");
                    return Page();
                }
            }

            // Verificar se já existe usuário com este email
            var usuarioExistente = await _userManager.FindByEmailAsync(Input.Email);
            if (usuarioExistente != null)
            {
                ModelState.AddModelError("Input.Email", "Já existe um usuário cadastrado com este email.");
                return Page();
            }

            // Criar novo usuário
            var novoUsuario = new ApplicationUser
            {
                UserName = Input.Email,
                Email = Input.Email,
                NomeCompleto = Input.NomeCompleto,
                TipoUsuario = Input.TipoUsuario,
                Ativo = true,
                DataCadastro = DateTime.Now,
                EmailConfirmed = true // Admin pode criar usuários já confirmados
            };

            // Se for psicólogo, associar
            if (Input.TipoUsuario == TipoUsuario.Psicologo && Input.PsicologoId.HasValue)
            {
                novoUsuario.PsicologoId = Input.PsicologoId.Value;
                novoUsuario.CRP = Input.CRP;
            }

            // Criar usuário
            var resultado = await _userManager.CreateAsync(novoUsuario, Input.Senha);
            if (!resultado.Succeeded)
            {
                foreach (var erro in resultado.Errors)
                {
                    ModelState.AddModelError(string.Empty, erro.Description);
                }
                return Page();
            }

            // Adicionar role
            string role = Input.TipoUsuario switch
            {
                TipoUsuario.Admin => "Admin",
                TipoUsuario.Psicologo => "Psicologo",
                TipoUsuario.Cliente => "Cliente",
                _ => "Cliente"
            };

            await _userManager.AddToRoleAsync(novoUsuario, role);

            // Se for cliente, criar paciente
            if (Input.TipoUsuario == TipoUsuario.Cliente)
            {
                var novoPaciente = new Paciente
                {
                    Nome = Input.NomeCompleto,
                    Email = Input.Email,
                    CPF = Input.CPF ?? string.Empty,
                    Telefone = Input.Telefone ?? string.Empty,
                    Ativo = true,
                    DataCadastro = DateTime.Now,
                    DataCriacao = DateTime.Now,
                    PsicoPontos = 0,
                    ConsultasRealizadas = 0,
                    ConsultasGratuitas = 0
                };

                _context.Pacientes.Add(novoPaciente);
                await _context.SaveChangesAsync();

                // Associar paciente ao usuário
                novoUsuario.PacienteId = novoPaciente.Id;
                await _userManager.UpdateAsync(novoUsuario);
            }

            // Registrar auditoria
            var adminAtual = await _userManager.GetUserAsync(User);
            if (adminAtual != null)
            {
                var detalhes = JsonSerializer.Serialize(new
                {
                    TipoUsuario = Input.TipoUsuario.ToString(),
                    Role = role,
                    CriadoPor = adminAtual.NomeCompleto,
                    PsicologoId = Input.TipoUsuario == TipoUsuario.Psicologo ? Input.PsicologoId : null,
                    PacienteId = Input.TipoUsuario == TipoUsuario.Cliente ? novoUsuario.PacienteId : null
                });

                await _auditoriaService.RegistrarAcaoAsync(
                    adminId: adminAtual.Id,
                    adminNome: adminAtual.NomeCompleto ?? adminAtual.Email ?? "Sistema",
                    usuarioAfetadoId: novoUsuario.Id,
                    usuarioAfetadoNome: novoUsuario.NomeCompleto,
                    usuarioAfetadoEmail: novoUsuario.Email ?? string.Empty,
                    acao: TipoAcaoAuditoria.CriacaoUsuario,
                    detalhes: detalhes,
                    ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString()
                );
            }

            TempData["SuccessMessage"] = $"Usuário {Input.NomeCompleto} criado com sucesso!";
            return RedirectToPage("/Admin/Usuarios");
        }

        private async Task CarregarPsicologosDisponiveisAsync()
        {
            // Carregar psicólogos que ainda não têm usuário associado
            var psicologosComUsuario = await _context.Users
                .Where(u => u.PsicologoId.HasValue)
                .Select(u => u.PsicologoId!.Value)
                .ToListAsync();

            PsicologosDisponiveis = await _context.Psicologos
                .Where(p => p.Ativo && !psicologosComUsuario.Contains(p.Id))
                .OrderBy(p => p.Nome)
                .ToListAsync();
        }
    }

    public class NovoUsuarioInputModel
    {
        [Required(ErrorMessage = "Nome completo é obrigatório")]
        [StringLength(200, ErrorMessage = "Nome deve ter no máximo 200 caracteres")]
        public string NomeCompleto { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email é obrigatório")]
        [EmailAddress(ErrorMessage = "Email deve ter um formato válido")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Senha é obrigatória")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Senha deve ter pelo menos 6 caracteres")]
        [DataType(DataType.Password)]
        public string Senha { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirmação de senha é obrigatória")]
        [Compare("Senha", ErrorMessage = "Senha e confirmação não conferem")]
        [DataType(DataType.Password)]
        public string ConfirmarSenha { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tipo de usuário é obrigatório")]
        public TipoUsuario TipoUsuario { get; set; }

        // Campos específicos para Cliente
        [StringLength(11, MinimumLength = 11, ErrorMessage = "CPF deve ter 11 dígitos")]
        [RegularExpression(@"^\d{11}$", ErrorMessage = "CPF deve conter apenas números")]
        public string? CPF { get; set; }

        [Phone(ErrorMessage = "Telefone deve ter um formato válido")]
        public string? Telefone { get; set; }

        // Campos específicos para Psicólogo
        public int? PsicologoId { get; set; }

        [StringLength(20, ErrorMessage = "CRP deve ter no máximo 20 caracteres")]
        public string? CRP { get; set; }
    }
}