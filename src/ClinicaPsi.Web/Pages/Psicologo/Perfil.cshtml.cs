using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using ClinicaPsi.Web.Extensions;
using System.Security.Claims;

namespace ClinicaPsi.Web.Pages.Psicologo
{
    [Authorize(Roles = "Admin,Psicologo")]
    public class PerfilModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public PerfilModel(AppDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public ClinicaPsi.Shared.Models.Psicologo Psicologo { get; set; } = new();
        public List<Consulta> ProximasConsultas { get; set; } = new();
        
        // Estatísticas
        public int TotalConsultas { get; set; }
        public int PacientesAtivos { get; set; }
        public decimal ReceitaTotal { get; set; }
        public double TaxaComparecimento { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Forbid();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user?.PsicologoId == null)
                return Forbid();

            var psicologoId = user.PsicologoId.Value;

            // Buscar dados do psicólogo
            var psicologo = await _context.Psicologos.FindAsync(psicologoId);
            if (psicologo == null)
                return NotFound();

            Psicologo = psicologo;

            // Buscar próximas consultas
            ProximasConsultas = await _context.Consultas
                .Include(c => c.Paciente)
                .Where(c => c.PsicologoId == psicologoId &&
                           c.DataHorario >= DateTime.Now &&
                           c.Status != StatusConsulta.Cancelada)
                .OrderBy(c => c.DataHorario)
                .Take(10)
                .ToListAsync();

            // Calcular estatísticas
            await CalcularEstatisticasAsync(psicologoId);

            return Page();
        }

        public async Task<IActionResult> OnPostAtualizarPerfilAsync(
            string nome,
            string email,
            string telefone,
            string especialidades,
            decimal valorConsulta)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Forbid();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user?.PsicologoId == null)
                return Forbid();

            try
            {
                var psicologo = await _context.Psicologos.FindAsync(user.PsicologoId.Value);
                if (psicologo == null)
                    return NotFound();

                // Atualizar dados
                psicologo.Nome = nome;
                psicologo.Telefone = telefone;
                psicologo.Especialidades = especialidades;
                psicologo.ValorConsulta = valorConsulta;

                // Atualizar email se mudou
                if (psicologo.Email != email)
                {
                    // Verificar se email já existe
                    var emailExiste = await _context.Users.AnyAsync(u => u.Email == email && u.Id != userId);
                    if (emailExiste)
                    {
                        ModelState.AddModelError("", "Este email já está sendo usado por outro usuário");
                        return await OnGetAsync();
                    }

                    psicologo.Email = email;
                    
                    // Atualizar email no Identity
                    var identityUser = await _userManager.FindByIdAsync(userId);
                    if (identityUser != null)
                    {
                        identityUser.Email = email;
                        identityUser.UserName = email;
                        await _userManager.UpdateAsync(identityUser);
                    }
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = "Perfil atualizado com sucesso!";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Erro ao atualizar perfil: " + ex.Message);
                return await OnGetAsync();
            }
        }

        public async Task<IActionResult> OnPostAtualizarHorariosAsync(
            string[] diasAtendimento,
            string[] periodosAtendimento)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Forbid();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user?.PsicologoId == null)
                return Forbid();

            try
            {
                var psicologo = await _context.Psicologos.FindAsync(user.PsicologoId.Value);
                if (psicologo == null)
                    return NotFound();

                // Resetar todos os dias e períodos
                psicologo.AtendeSegunda = false;
                psicologo.AtendeTerca = false;
                psicologo.AtendeQuarta = false;
                psicologo.AtendeQuinta = false;
                psicologo.AtendeSexta = false;
                psicologo.AtendeSabado = false;
                psicologo.AtendeDomingo = false;
                psicologo.AtendeManha = false;
                psicologo.AtendeTarde = false;

                // Definir dias selecionados
                if (diasAtendimento != null)
                {
                    foreach (var dia in diasAtendimento)
                    {
                        switch (dia)
                        {
                            case "Segunda":
                                psicologo.AtendeSegunda = true;
                                break;
                            case "Terca":
                                psicologo.AtendeTerca = true;
                                break;
                            case "Quarta":
                                psicologo.AtendeQuarta = true;
                                break;
                            case "Quinta":
                                psicologo.AtendeQuinta = true;
                                break;
                            case "Sexta":
                                psicologo.AtendeSexta = true;
                                break;
                            case "Sabado":
                                psicologo.AtendeSabado = true;
                                break;
                            case "Domingo":
                                psicologo.AtendeDomingo = true;
                                break;
                        }
                    }
                }

                // Definir períodos selecionados
                if (periodosAtendimento != null)
                {
                    foreach (var periodo in periodosAtendimento)
                    {
                        switch (periodo)
                        {
                            case "Manha":
                                psicologo.AtendeManha = true;
                                break;
                            case "Tarde":
                                psicologo.AtendeTarde = true;
                                break;
                        }
                    }
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = "Horários de atendimento atualizados com sucesso!";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Erro ao atualizar horários: " + ex.Message);
                return await OnGetAsync();
            }
        }

        public async Task<IActionResult> OnPostAlterarSenhaAsync(
            string senhaAtual,
            string novaSenha,
            string confirmarSenha)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Forbid();

            try
            {
                if (novaSenha != confirmarSenha)
                {
                    ModelState.AddModelError("", "A nova senha e a confirmação não coincidem");
                    return await OnGetAsync();
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return NotFound();

                var result = await _userManager.ChangePasswordAsync(user, senhaAtual, novaSenha);

                if (result.Succeeded)
                {
                    TempData["Success"] = "Senha alterada com sucesso!";
                    return RedirectToPage();
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }
                    return await OnGetAsync();
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Erro ao alterar senha: " + ex.Message);
                return await OnGetAsync();
            }
        }

        private async Task CalcularEstatisticasAsync(int psicologoId)
        {
            var dataAtual = DateTime.Now;
            var data30DiasAtras = dataAtual.AddDays(-30);

            // Total de consultas
            TotalConsultas = await _context.Consultas
                .Where(c => c.PsicologoId == psicologoId)
                .CountAsync();

            // Pacientes ativos (últimos 30 dias)
            PacientesAtivos = await _context.Consultas
                .Where(c => c.PsicologoId == psicologoId && c.DataHorario >= data30DiasAtras)
                .Select(c => c.PacienteId)
                .Distinct()
                .CountAsync();

            // Receita total
            ReceitaTotal = await _context.Consultas
                .Where(c => c.PsicologoId == psicologoId && c.Status == StatusConsulta.Realizada)
                .SumAsync(c => c.Valor);

            // Taxa de comparecimento
            var consultasComparencia = await _context.Consultas
                .Where(c => c.PsicologoId == psicologoId &&
                           (c.Status == StatusConsulta.Realizada || 
                            c.Status == StatusConsulta.NoShow ||
                            c.Status == StatusConsulta.Cancelada))
                .CountAsync();

            var consultasRealizadas = await _context.Consultas
                .Where(c => c.PsicologoId == psicologoId && c.Status == StatusConsulta.Realizada)
                .CountAsync();

            TaxaComparecimento = consultasComparencia > 0 
                ? (double)consultasRealizadas / consultasComparencia * 100
                : 0;
        }
    }
}