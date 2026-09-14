using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ClinicaPsi.Shared.Models;
using ClinicaPsi.Application.Services;
using System.ComponentModel.DataAnnotations;
using PsicologoEntity = ClinicaPsi.Shared.Models.Psicologo;

namespace ClinicaPsi.Web.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class PsicologosModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly PsicologoService _psicologoService;
        private readonly UsuarioPsicologoSyncService _syncService;
        private readonly ILogger<PsicologosModel> _logger;

        public PsicologosModel(
            UserManager<ApplicationUser> userManager,
            PsicologoService psicologoService,
            UsuarioPsicologoSyncService syncService,
            ILogger<PsicologosModel> logger)
        {
            _userManager = userManager;
            _psicologoService = psicologoService;
            _syncService = syncService;
            _logger = logger;
        }

        public List<PsicologoEntity> Psicologos { get; set; } = new();
        public string? SearchTerm { get; set; }
        public string? EspecialidadeFiltro { get; set; }
        public bool? StatusFiltro { get; set; }

        [BindProperty]
        public NovoPsicologoModel NovoPsicologo { get; set; } = new();

        public class NovoPsicologoModel
        {
            [Required(ErrorMessage = "Nome é obrigatório")]
            public string Nome { get; set; } = string.Empty;

            [Required(ErrorMessage = "Email é obrigatório")]
            [EmailAddress(ErrorMessage = "Email inválido")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "CRP é obrigatório")]
            public string CRP { get; set; } = string.Empty;

            [Required(ErrorMessage = "Telefone é obrigatório")]
            public string Telefone { get; set; } = string.Empty;

            [Required(ErrorMessage = "Valor da consulta é obrigatório")]
            [Range(0.01, 9999.99, ErrorMessage = "Valor deve ser maior que zero")]
            public decimal ValorConsulta { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(string? searchTerm, string? especialidade, bool? status)
        {
            SearchTerm = searchTerm;
            EspecialidadeFiltro = especialidade;
            StatusFiltro = status;

            try
            {
                var sync = await _syncService.SincronizarTodosAsync();
                if (sync.UsuariosCriados > 0)
                {
                    TempData["SuccessMessage"] =
                        $"Sincronização: {sync.UsuariosCriados} usuário(s) criado(s) para psicólogos sem login. " +
                        "Defina novas senhas em Gestão de Usuários se necessário.";
                }

                var psicologos = (await _psicologoService.GetAllIncludingInactiveAsync()).AsQueryable();

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    psicologos = psicologos.Where(p =>
                        p.Nome.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                        p.CRP.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                        (p.Especialidades != null && p.Especialidades.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)));
                }

                if (!string.IsNullOrEmpty(especialidade))
                {
                    psicologos = psicologos.Where(p =>
                        p.Especialidades != null &&
                        p.Especialidades.Contains(especialidade, StringComparison.OrdinalIgnoreCase));
                }

                if (status.HasValue)
                    psicologos = psicologos.Where(p => p.Ativo == status.Value);

                Psicologos = psicologos.OrderBy(p => p.Nome).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar psicólogos");
                TempData["ErrorMessage"] = "Erro ao carregar lista de psicólogos.";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAddPsicologoAsync(string senha, string[] especialidades)
        {
            _logger.LogInformation("Iniciando cadastro de psicólogo: {Email}", NovoPsicologo?.Email);

            if (!ModelState.IsValid)
            {
                var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                TempData["ErrorMessage"] = $"Dados inválidos: {errors}";
                await OnGetAsync(null, null, null);
                return Page();
            }

            if (string.IsNullOrEmpty(senha) || senha.Length < 6)
            {
                TempData["ErrorMessage"] = "A senha deve ter no mínimo 6 caracteres.";
                await OnGetAsync(null, null, null);
                return Page();
            }

            if (especialidades == null || especialidades.Length == 0)
            {
                TempData["ErrorMessage"] = "Selecione pelo menos uma especialidade.";
                await OnGetAsync(null, null, null);
                return Page();
            }

            try
            {
                var emailExistente = await _userManager.FindByEmailAsync(NovoPsicologo.Email);
                if (emailExistente != null)
                {
                    TempData["ErrorMessage"] = "Já existe um usuário com este email.";
                    await OnGetAsync(null, null, null);
                    return Page();
                }

                var psicologo = new PsicologoEntity
                {
                    Nome = NovoPsicologo.Nome,
                    Email = NovoPsicologo.Email,
                    CRP = NovoPsicologo.CRP,
                    Telefone = NovoPsicologo.Telefone,
                    ValorConsulta = NovoPsicologo.ValorConsulta,
                    Especialidades = string.Join(", ", especialidades),
                    Ativo = true,
                    DataCadastro = DateTime.UtcNow,
                    DataCriacao = DateTime.UtcNow,
                    HorarioInicioManha = new TimeSpan(8, 0, 0),
                    HorarioFimManha = new TimeSpan(12, 0, 0),
                    HorarioInicioTarde = new TimeSpan(14, 0, 0),
                    HorarioFimTarde = new TimeSpan(18, 0, 0),
                    AtendeSegunda = true,
                    AtendeTerca = true,
                    AtendeQuarta = true,
                    AtendeQuinta = true,
                    AtendeSexta = true,
                    AtendeSabado = false,
                    AtendeDomingo = false
                };

                await _psicologoService.CreateAsync(psicologo);

                var user = new ApplicationUser
                {
                    UserName = NovoPsicologo.Email,
                    Email = NovoPsicologo.Email,
                    NomeCompleto = NovoPsicologo.Nome,
                    TipoUsuario = TipoUsuario.Psicologo,
                    CRP = NovoPsicologo.CRP,
                    PsicologoId = psicologo.Id,
                    EmailConfirmed = true,
                    Ativo = true,
                    DataCadastro = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(user, senha);
                if (!result.Succeeded)
                {
                    await _psicologoService.DeleteAsync(psicologo.Id);
                    var errorMessages = string.Join("; ", result.Errors.Select(e => e.Description));
                    TempData["ErrorMessage"] = $"Erro ao criar usuário: {errorMessages}";
                    await OnGetAsync(null, null, null);
                    return Page();
                }

                await _userManager.AddToRoleAsync(user, "Psicologo");
                await _syncService.VincularAsync(user, psicologo);

                TempData["SuccessMessage"] = $"Psicólogo {psicologo.Nome} cadastrado com sucesso!";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar psicólogo: {Message}", ex.Message);
                TempData["ErrorMessage"] = $"Erro ao criar psicólogo: {ex.Message}";
                await OnGetAsync(null, null, null);
                return Page();
            }
        }

        public async Task<IActionResult> OnPostToggleStatusAsync(int id)
        {
            try
            {
                var psicologo = await _psicologoService.GetByIdAsync(id);
                if (psicologo == null)
                {
                    TempData["ErrorMessage"] = "Psicólogo não encontrado.";
                    return RedirectToPage();
                }

                var novoStatus = !psicologo.Ativo;
                await _syncService.SincronizarStatusPorPsicologoIdAsync(id, novoStatus);
                TempData["SuccessMessage"] = $"Psicólogo {(novoStatus ? "ativado" : "desativado")} com sucesso!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao alternar status do psicólogo {PsicologoId}", id);
                TempData["ErrorMessage"] = "Erro ao alterar status do psicólogo.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            try
            {
                var psicologo = await _psicologoService.GetByIdAsync(id);
                if (psicologo == null)
                {
                    TempData["ErrorMessage"] = "Psicólogo não encontrado.";
                    return RedirectToPage();
                }

                var nome = psicologo.Nome;
                await _psicologoService.DeleteAsync(id);
                await _syncService.SincronizarStatusPorPsicologoIdAsync(id, ativo: false);
                TempData["SuccessMessage"] = $"Psicólogo {nome} excluído com sucesso.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao excluir psicólogo {PsicologoId}", id);
                TempData["ErrorMessage"] = "Erro ao excluir psicólogo. Verifique se há vínculos que impedem a exclusão.";
            }

            return RedirectToPage();
        }
    }
}
