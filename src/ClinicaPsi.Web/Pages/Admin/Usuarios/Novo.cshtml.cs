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
        private readonly ILogger<NovoModel> _logger;

        public NovoModel(
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            AuditoriaService auditoriaService,
            ILogger<NovoModel> logger)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _auditoriaService = auditoriaService;
            _logger = logger;
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

            // CPF/Telefone opcionais: limpar vazios para não falhar validação de formato
            if (string.IsNullOrWhiteSpace(Input.CPF))
            {
                Input.CPF = null;
                ModelState.Remove("Input.CPF");
            }

            if (string.IsNullOrWhiteSpace(Input.Telefone))
            {
                Input.Telefone = null;
                ModelState.Remove("Input.Telefone");
            }

            if (!Input.TipoUsuario.HasValue)
            {
                ModelState.AddModelError("Input.TipoUsuario", "Tipo de usuário é obrigatório");
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var tipoUsuario = Input.TipoUsuario!.Value;

            // Validações específicas
            if (tipoUsuario == TipoUsuario.Psicologo)
            {
                if (!Input.PsicologoId.HasValue)
                {
                    ModelState.AddModelError("Input.PsicologoId", "Selecione um psicólogo para associar ao usuário.");
                    return Page();
                }

                if (!PsicologosDisponiveis.Any(p => p.Id == Input.PsicologoId.Value))
                {
                    ModelState.AddModelError("Input.PsicologoId", "O psicólogo selecionado não está disponível.");
                    return Page();
                }
            }

            if (tipoUsuario == TipoUsuario.Cliente && !string.IsNullOrWhiteSpace(Input.CPF))
            {
                var pacienteExistente = await _context.Pacientes.FirstOrDefaultAsync(p => p.CPF == Input.CPF);
                if (pacienteExistente != null)
                {
                    ModelState.AddModelError("Input.CPF", "Já existe um paciente cadastrado com este CPF.");
                    return Page();
                }
            }

            var usuarioExistente = await _userManager.FindByEmailAsync(Input.Email);
            if (usuarioExistente != null)
            {
                ModelState.AddModelError("Input.Email", "Já existe um usuário cadastrado com este email.");
                return Page();
            }

            try
            {
                var novoUsuario = new ApplicationUser
                {
                    UserName = Input.Email,
                    Email = Input.Email,
                    NomeCompleto = Input.NomeCompleto.Trim(),
                    TipoUsuario = tipoUsuario,
                    Ativo = true,
                    DataCadastro = DateTime.UtcNow,
                    EmailConfirmed = true
                };

                if (tipoUsuario == TipoUsuario.Psicologo && Input.PsicologoId.HasValue)
                {
                    var psicologo = await _context.Psicologos.FindAsync(Input.PsicologoId.Value);
                    if (psicologo == null)
                    {
                        ModelState.AddModelError("Input.PsicologoId", "Psicólogo não encontrado.");
                        return Page();
                    }

                    novoUsuario.PsicologoId = psicologo.Id;
                    novoUsuario.CRP = string.IsNullOrWhiteSpace(Input.CRP) ? psicologo.CRP : Input.CRP;
                }

                var resultado = await _userManager.CreateAsync(novoUsuario, Input.Senha);
                if (!resultado.Succeeded)
                {
                    foreach (var erro in resultado.Errors)
                    {
                        ModelState.AddModelError(string.Empty, erro.Description);
                    }
                    return Page();
                }

                string role = tipoUsuario switch
                {
                    TipoUsuario.Admin => "Admin",
                    TipoUsuario.Psicologo => "Psicologo",
                    TipoUsuario.Cliente => "Cliente",
                    _ => "Cliente"
                };

                if (!await _roleManager.RoleExistsAsync(role))
                {
                    await _roleManager.CreateAsync(new IdentityRole(role));
                }

                await _userManager.AddToRoleAsync(novoUsuario, role);

                // Vincular psicólogo ↔ usuário (sincronização bidirecional)
                if (tipoUsuario == TipoUsuario.Psicologo && Input.PsicologoId.HasValue)
                {
                    var psicologo = await _context.Psicologos.FindAsync(Input.PsicologoId.Value);
                    if (psicologo != null)
                    {
                        psicologo.UserId = novoUsuario.Id;
                        psicologo.DataAtualizacao = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                }

                if (tipoUsuario == TipoUsuario.Cliente)
                {
                    var novoPaciente = new Paciente
                    {
                        Nome = Input.NomeCompleto.Trim(),
                        Email = Input.Email,
                        CPF = Input.CPF ?? string.Empty,
                        Telefone = Input.Telefone ?? string.Empty,
                        Ativo = true,
                        DataCadastro = DateTime.UtcNow,
                        DataCriacao = DateTime.UtcNow,
                        PsicoPontos = 0,
                        ConsultasRealizadas = 0,
                        ConsultasGratuitas = 0
                    };

                    _context.Pacientes.Add(novoPaciente);
                    await _context.SaveChangesAsync();

                    novoUsuario.PacienteId = novoPaciente.Id;
                    await _userManager.UpdateAsync(novoUsuario);
                }

                var adminAtual = await _userManager.GetUserAsync(User);
                if (adminAtual != null)
                {
                    var detalhes = JsonSerializer.Serialize(new
                    {
                        TipoUsuario = tipoUsuario.ToString(),
                        Role = role,
                        CriadoPor = adminAtual.NomeCompleto,
                        PsicologoId = tipoUsuario == TipoUsuario.Psicologo ? Input.PsicologoId : null,
                        PacienteId = tipoUsuario == TipoUsuario.Cliente ? novoUsuario.PacienteId : null
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar usuário {Email}", Input.Email);
                ModelState.AddModelError(string.Empty, $"Erro ao criar usuário: {ex.Message}");
                return Page();
            }
        }

        private async Task CarregarPsicologosDisponiveisAsync()
        {
            var psicologosComUsuario = await _context.Users
                .Where(u => u.PsicologoId.HasValue)
                .Select(u => u.PsicologoId!.Value)
                .ToListAsync();

            var psicologosComUserId = await _context.Psicologos
                .Where(p => p.UserId != null && p.UserId != "")
                .Select(p => p.Id)
                .ToListAsync();

            var ocupados = psicologosComUsuario.Union(psicologosComUserId).ToHashSet();

            PsicologosDisponiveis = await _context.Psicologos
                .Where(p => p.Ativo && !ocupados.Contains(p.Id))
                .OrderBy(p => p.Nome)
                .ToListAsync();
        }
    }

    public class NovoUsuarioInputModel
    {
        [Required(ErrorMessage = "Nome completo é obrigatório")]
        [StringLength(200, ErrorMessage = "Nome deve ter no máximo 200 caracteres")]
        [Display(Name = "Nome Completo")]
        public string NomeCompleto { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email é obrigatório")]
        [EmailAddress(ErrorMessage = "Email deve ter um formato válido")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Senha é obrigatória")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Senha deve ter pelo menos 6 caracteres")]
        [DataType(DataType.Password)]
        [Display(Name = "Senha")]
        public string Senha { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirmação de senha é obrigatória")]
        [Compare("Senha", ErrorMessage = "Senha e confirmação não conferem")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirmar Senha")]
        public string ConfirmarSenha { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tipo de usuário é obrigatório")]
        [Display(Name = "Tipo de Usuário")]
        public TipoUsuario? TipoUsuario { get; set; }

        [RegularExpression(@"^$|^\d{11}$", ErrorMessage = "CPF deve conter exatamente 11 dígitos numéricos")]
        [Display(Name = "CPF")]
        public string? CPF { get; set; }

        [Display(Name = "Telefone")]
        public string? Telefone { get; set; }

        [Display(Name = "Psicólogo")]
        public int? PsicologoId { get; set; }

        [StringLength(20, ErrorMessage = "CRP deve ter no máximo 20 caracteres")]
        [Display(Name = "CRP")]
        public string? CRP { get; set; }
    }
}
