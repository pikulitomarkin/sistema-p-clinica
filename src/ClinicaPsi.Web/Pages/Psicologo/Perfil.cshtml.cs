using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using ClinicaPsi.Web.Extensions;
using ClinicaPsi.Web.Services;
using System.Security.Claims;

namespace ClinicaPsi.Web.Pages.Psicologo
{
    [Authorize(Roles = "Admin,Psicologo")]
    public class PerfilModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly FotoPerfilService _fotoPerfilService;

        public PerfilModel(
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            FotoPerfilService fotoPerfilService)
        {
            _context = context;
            _userManager = userManager;
            _fotoPerfilService = fotoPerfilService;
        }

        public ClinicaPsi.Shared.Models.Psicologo Psicologo { get; set; } = new();
        public List<Consulta> ProximasConsultas { get; set; } = new();
        public string? FotoUrl { get; set; }

        public int TotalConsultas { get; set; }
        public int PacientesAtivos { get; set; }
        public decimal ReceitaTotal { get; set; }
        public double TaxaComparecimento { get; set; }

        [BindProperty]
        public IFormFile? FotoArquivo { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                    return Forbid();

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user?.PsicologoId == null)
                    return Forbid();

                var psicologoId = user.PsicologoId.Value;
                FotoUrl = user.FotoUrl;

                var psicologo = await _context.Psicologos.FindAsync(psicologoId);
                if (psicologo == null)
                    return NotFound();

                Psicologo = psicologo;
                if (string.IsNullOrWhiteSpace(FotoUrl))
                    FotoUrl = psicologo.FotoUrl;

                ProximasConsultas = await _context.Consultas
                    .Include(c => c.Paciente)
                    .Where(c => c.PsicologoId == psicologoId &&
                               c.DataHorario >= DateTime.Now &&
                               c.Status != StatusConsulta.Cancelada)
                    .OrderBy(c => c.DataHorario)
                    .Take(10)
                    .ToListAsync();

                await CalcularEstatisticasAsync(psicologoId);

                return Page();
            }
            catch (Exception)
            {
                TempData["Error"] = "A página está sendo atualizada. Por favor, aguarde alguns minutos e recarregue.";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostUploadFotoAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Forbid();

            var user = await _userManager.FindByIdAsync(userId);
            if (user?.PsicologoId == null)
                return Forbid();

            var (ok, url, erro) = await _fotoPerfilService.SalvarAsync(FotoArquivo!, user.FotoUrl);
            if (!ok)
            {
                TempData["Error"] = erro ?? "Falha no upload da foto.";
                return RedirectToPage();
            }

            user.FotoUrl = url;
            await _userManager.UpdateAsync(user);

            var psicologo = await _context.Psicologos.FindAsync(user.PsicologoId.Value);
            if (psicologo != null)
            {
                psicologo.FotoUrl = url;
                psicologo.DataAtualizacao = DateTime.Now;
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "Foto de perfil atualizada com sucesso!";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRemoverFotoAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Forbid();

            var user = await _userManager.FindByIdAsync(userId);
            if (user?.PsicologoId == null)
                return Forbid();

            var urlAnterior = user.FotoUrl;
            _fotoPerfilService.RemoverArquivoFisico(urlAnterior);
            user.FotoUrl = null;
            await _userManager.UpdateAsync(user);

            var psicologo = await _context.Psicologos.FindAsync(user.PsicologoId.Value);
            if (psicologo != null)
            {
                if (!string.IsNullOrWhiteSpace(psicologo.FotoUrl) && psicologo.FotoUrl != urlAnterior)
                    _fotoPerfilService.RemoverArquivoFisico(psicologo.FotoUrl);
                psicologo.FotoUrl = null;
                psicologo.DataAtualizacao = DateTime.Now;
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "Foto de perfil removida.";
            return RedirectToPage();
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

                psicologo.Nome = nome;
                psicologo.Telefone = telefone;
                psicologo.Especialidades = especialidades;
                psicologo.ValorConsulta = valorConsulta;

                if (psicologo.Email != email)
                {
                    var emailExiste = await _context.Users.AnyAsync(u => u.Email == email && u.Id != userId);
                    if (emailExiste)
                    {
                        ModelState.AddModelError("", "Este email já está sendo usado por outro usuário");
                        return await OnGetAsync();
                    }

                    psicologo.Email = email;

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

                psicologo.AtendeSegunda = false;
                psicologo.AtendeTerca = false;
                psicologo.AtendeQuarta = false;
                psicologo.AtendeQuinta = false;
                psicologo.AtendeSexta = false;
                psicologo.AtendeSabado = false;
                psicologo.AtendeDomingo = false;
                psicologo.AtendeManha = false;
                psicologo.AtendeTarde = false;

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

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
                return await OnGetAsync();
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

            TotalConsultas = await _context.Consultas
                .Where(c => c.PsicologoId == psicologoId)
                .CountAsync();

            PacientesAtivos = await _context.Consultas
                .Where(c => c.PsicologoId == psicologoId && c.DataHorario >= data30DiasAtras)
                .Select(c => c.PacienteId)
                .Distinct()
                .CountAsync();

            ReceitaTotal = await _context.Consultas
                .Where(c => c.PsicologoId == psicologoId && c.Status == StatusConsulta.Realizada)
                .SumAsync(c => c.Valor);

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
