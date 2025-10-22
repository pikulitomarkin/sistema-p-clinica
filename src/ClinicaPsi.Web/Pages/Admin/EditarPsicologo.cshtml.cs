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
    public class EditarPsicologoModel : PageModel
    {
        private readonly PsicologoService _psicologoService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<EditarPsicologoModel> _logger;

        public EditarPsicologoModel(
            PsicologoService psicologoService,
            UserManager<ApplicationUser> userManager,
            ILogger<EditarPsicologoModel> logger)
        {
            _psicologoService = psicologoService;
            _userManager = userManager;
            _logger = logger;
        }

        [BindProperty]
        public PsicologoEntity Psicologo { get; set; } = new();

        public ApplicationUser? UsuarioVinculado { get; set; }
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            try
            {
                var psicologo = await _psicologoService.GetByIdAsync(id);
                if (psicologo == null)
                {
                    TempData["ErrorMessage"] = "Psicólogo não encontrado.";
                    return RedirectToPage("/Admin/Psicologos");
                }

                Psicologo = psicologo;

                // Buscar usuário vinculado
                var usuarios = _userManager.Users.Where(u => u.PsicologoId == id).ToList();
                if (usuarios.Any())
                {
                    UsuarioVinculado = usuarios.First();
                }

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar psicólogo {PsicologoId}", id);
                TempData["ErrorMessage"] = "Erro ao carregar dados do psicólogo.";
                return RedirectToPage("/Admin/Psicologos");
            }
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            if (!ModelState.IsValid)
            {
                ErrorMessage = "Por favor, corrija os erros no formulário.";
                await OnGetAsync(id);
                return Page();
            }

            try
            {
                // Buscar psicólogo atual
                var psicologoAtual = await _psicologoService.GetByIdAsync(id);
                if (psicologoAtual == null)
                {
                    TempData["ErrorMessage"] = "Psicólogo não encontrado.";
                    return RedirectToPage("/Admin/Psicologos");
                }

                // Atualizar campos
                psicologoAtual.Nome = Psicologo.Nome;
                psicologoAtual.Email = Psicologo.Email;
                psicologoAtual.CRP = Psicologo.CRP;
                psicologoAtual.Telefone = Psicologo.Telefone;
                psicologoAtual.ValorConsulta = Psicologo.ValorConsulta;
                psicologoAtual.Especialidades = Psicologo.Especialidades;
                psicologoAtual.Ativo = Psicologo.Ativo;

                // Horários
                psicologoAtual.HorarioInicioManha = Psicologo.HorarioInicioManha;
                psicologoAtual.HorarioFimManha = Psicologo.HorarioFimManha;
                psicologoAtual.HorarioInicioTarde = Psicologo.HorarioInicioTarde;
                psicologoAtual.HorarioFimTarde = Psicologo.HorarioFimTarde;
                psicologoAtual.AtendeManha = Psicologo.AtendeManha;
                psicologoAtual.AtendeTarde = Psicologo.AtendeTarde;

                // Dias de atendimento
                psicologoAtual.AtendeSegunda = Psicologo.AtendeSegunda;
                psicologoAtual.AtendeTerca = Psicologo.AtendeTerca;
                psicologoAtual.AtendeQuarta = Psicologo.AtendeQuarta;
                psicologoAtual.AtendeQuinta = Psicologo.AtendeQuinta;
                psicologoAtual.AtendeSexta = Psicologo.AtendeSexta;
                psicologoAtual.AtendeSabado = Psicologo.AtendeSabado;
                psicologoAtual.AtendeDomingo = Psicologo.AtendeDomingo;

                psicologoAtual.DataAtualizacao = DateTime.Now;

                // Salvar
                await _psicologoService.UpdateAsync(psicologoAtual);

                // Atualizar dados do usuário vinculado, se existir
                var usuario = _userManager.Users.FirstOrDefault(u => u.PsicologoId == id);
                if (usuario != null)
                {
                    // Atualizar email do usuário se mudou
                    if (usuario.Email != Psicologo.Email)
                    {
                        usuario.Email = Psicologo.Email;
                        usuario.UserName = Psicologo.Email;
                        usuario.NormalizedEmail = Psicologo.Email.ToUpper();
                        usuario.NormalizedUserName = Psicologo.Email.ToUpper();
                        await _userManager.UpdateAsync(usuario);
                    }

                    // Atualizar CRP
                    if (usuario.CRP != Psicologo.CRP)
                    {
                        usuario.CRP = Psicologo.CRP;
                        await _userManager.UpdateAsync(usuario);
                    }

                    // Sincronizar status ativo/inativo
                    if (usuario.Ativo != Psicologo.Ativo)
                    {
                        usuario.Ativo = Psicologo.Ativo;
                        await _userManager.UpdateAsync(usuario);
                    }
                }

                TempData["SuccessMessage"] = "Psicólogo atualizado com sucesso!";
                return RedirectToPage("/Admin/Psicologos");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar psicólogo {PsicologoId}", id);
                ErrorMessage = "Erro ao salvar alterações. Por favor, tente novamente.";
                await OnGetAsync(id);
                return Page();
            }
        }
    }
}
