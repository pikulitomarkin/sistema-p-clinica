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
        private readonly ILogger<PsicologosModel> _logger;

        public PsicologosModel(
            UserManager<ApplicationUser> userManager,
            PsicologoService psicologoService,
            ILogger<PsicologosModel> logger)
        {
            _userManager = userManager;
            _psicologoService = psicologoService;
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
                var psicologos = (await _psicologoService.GetAllAsync()).AsQueryable();

                // Aplicar filtros
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    psicologos = psicologos.Where(p => 
                        p.Nome.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                        p.CRP.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                        p.Especialidades.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrEmpty(especialidade))
                {
                    psicologos = psicologos.Where(p => 
                        p.Especialidades.Contains(especialidade, StringComparison.OrdinalIgnoreCase));
                }

                if (status.HasValue)
                {
                    psicologos = psicologos.Where(p => p.Ativo == status.Value);
                }

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
            if (!ModelState.IsValid)
            {
                await OnGetAsync(null, null, null);
                return Page();
            }

            try
            {
                // Criar usuário no Identity
                var user = new ApplicationUser
                {
                    UserName = NovoPsicologo.Email,
                    Email = NovoPsicologo.Email,
                    TipoUsuario = TipoUsuario.Psicologo,
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(user, senha);
                if (!result.Succeeded)
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    await OnGetAsync(null, null, null);
                    return Page();
                }

                // Adicionar à role
                await _userManager.AddToRoleAsync(user, "Psicologo");

                // Criar psicólogo
                var psicologo = new PsicologoEntity
                {
                    Nome = NovoPsicologo.Nome,
                    Email = NovoPsicologo.Email,
                    CRP = NovoPsicologo.CRP,
                    Telefone = NovoPsicologo.Telefone,
                    ValorConsulta = NovoPsicologo.ValorConsulta,
                    Especialidades = string.Join(", ", especialidades),
                    Ativo = true,
                    DataCadastro = DateTime.Now,
                    // Horários padrão
                    HorarioInicioManha = new TimeSpan(8, 0, 0),
                    HorarioFimManha = new TimeSpan(12, 0, 0),
                    HorarioInicioTarde = new TimeSpan(14, 0, 0),
                    HorarioFimTarde = new TimeSpan(18, 0, 0),
                    // Dias padrão (segunda a sexta)
                    AtendeSegunda = true,
                    AtendeTerca = true,
                    AtendeQuarta = true,
                    AtendeQuinta = true,
                    AtendeSexta = true,
                    AtendeSabado = false,
                    AtendeDomingo = false
                };

                await _psicologoService.CreateAsync(psicologo);

                TempData["SuccessMessage"] = "Psicólogo cadastrado com sucesso!";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar psicólogo");
                ModelState.AddModelError(string.Empty, "Erro interno do servidor.");
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

                // Alternar status
                psicologo.Ativo = !psicologo.Ativo;
                psicologo.DataAtualizacao = DateTime.Now;
                await _psicologoService.UpdateAsync(psicologo);

                // Atualizar status do usuário vinculado, se existir
                var usuario = _userManager.Users.FirstOrDefault(u => u.PsicologoId == id);
                if (usuario != null)
                {
                    usuario.Ativo = psicologo.Ativo;
                    await _userManager.UpdateAsync(usuario);
                }

                TempData["SuccessMessage"] = $"Psicólogo {(psicologo.Ativo ? "ativado" : "desativado")} com sucesso!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao alternar status do psicólogo {PsicologoId}", id);
                TempData["ErrorMessage"] = "Erro ao alterar status do psicólogo.";
            }

            return RedirectToPage();
        }
    }
}