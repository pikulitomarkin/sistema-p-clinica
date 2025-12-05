using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ClinicaPsi.Application.Services;
using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;

namespace ClinicaPsi.Web.Pages.Psicologo
{
    [Authorize(Roles = "Psicologo")]
    public class DocumentosModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly PdfService _pdfService;

        public DocumentosModel(
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            PdfService pdfService)
        {
            _context = context;
            _userManager = userManager;
            _pdfService = pdfService;
        }

        public List<Paciente> Pacientes { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return RedirectToPage("/Account/Login");
                }

                // Buscar psicólogo logado
                var psicologo = await _context.Psicologos
                    .FirstOrDefaultAsync(p => p.UserId == user.Id);

                if (psicologo == null)
                {
                    ErrorMessage = "Psicólogo não encontrado no sistema.";
                    return Page();
                }

                // Buscar todos os pacientes que já tiveram consultas com este psicólogo
                Pacientes = await _context.Pacientes
                    .Where(p => _context.Consultas.Any(c => c.PacienteId == p.Id && c.PsicologoId == psicologo.Id))
                    .OrderBy(p => p.Nome)
                    .ToListAsync();

                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Erro ao carregar pacientes: {ex.Message}";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostGerarDeclaracaoAsync(
            int pacienteId,
            string dataConsulta,
            string horaConsulta,
            int duracao)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return RedirectToPage("/Account/Login");
                }

                var psicologo = await _context.Psicologos
                    .FirstOrDefaultAsync(p => p.UserId == user.Id);

                if (psicologo == null)
                {
                    ErrorMessage = "Psicólogo não encontrado.";
                    return await OnGetAsync();
                }

                var paciente = await _context.Pacientes
                    .FirstOrDefaultAsync(p => p.Id == pacienteId);

                if (paciente == null)
                {
                    ErrorMessage = "Paciente não encontrado.";
                    return await OnGetAsync();
                }

                // Combinar data e hora
                if (!DateTime.TryParse(dataConsulta, out var data))
                {
                    ErrorMessage = "Data inválida.";
                    return await OnGetAsync();
                }

                if (!TimeSpan.TryParse(horaConsulta, out var hora))
                {
                    ErrorMessage = "Horário inválido.";
                    return await OnGetAsync();
                }

                var dataHorario = data.Date.Add(hora);

                // Gerar PDF com dados manuais
                var pdfBytes = await _pdfService.GerarDeclaracaoComparecimentoManualAsync(
                    paciente,
                    psicologo,
                    dataHorario,
                    duracao);
                
                var nomeArquivo = $"Declaracao_{paciente.Nome.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.pdf";
                
                return File(pdfBytes, "application/pdf", nomeArquivo);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Erro ao gerar declaração: {ex.Message}";
                return await OnGetAsync();
            }
        }

        public async Task<IActionResult> OnPostGerarAtestadoAsync(
            int pacienteId,
            string dataConsulta,
            string horaConsulta,
            string? cid, 
            int diasAfastamento, 
            string? observacoes)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return RedirectToPage("/Account/Login");
                }

                var psicologo = await _context.Psicologos
                    .FirstOrDefaultAsync(p => p.UserId == user.Id);

                if (psicologo == null)
                {
                    ErrorMessage = "Psicólogo não encontrado.";
                    return await OnGetAsync();
                }

                var paciente = await _context.Pacientes
                    .FirstOrDefaultAsync(p => p.Id == pacienteId);

                if (paciente == null)
                {
                    ErrorMessage = "Paciente não encontrado.";
                    return await OnGetAsync();
                }

                if (diasAfastamento < 1 || diasAfastamento > 90)
                {
                    ErrorMessage = "O período de afastamento deve ser entre 1 e 90 dias.";
                    return await OnGetAsync();
                }

                // Combinar data e hora
                if (!DateTime.TryParse(dataConsulta, out var data))
                {
                    ErrorMessage = "Data inválida.";
                    return await OnGetAsync();
                }

                if (!TimeSpan.TryParse(horaConsulta, out var hora))
                {
                    ErrorMessage = "Horário inválido.";
                    return await OnGetAsync();
                }

                var dataHorario = data.Date.Add(hora);

                // Gerar PDF com dados manuais
                var pdfBytes = await _pdfService.GerarAtestadoManualAsync(
                    paciente,
                    psicologo,
                    dataHorario,
                    cid ?? string.Empty, 
                    diasAfastamento, 
                    observacoes ?? string.Empty);
                
                var nomeArquivo = $"Atestado_{paciente.Nome.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.pdf";
                
                return File(pdfBytes, "application/pdf", nomeArquivo);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Erro ao gerar atestado: {ex.Message}";
                return await OnGetAsync();
            }
        }
    }
}
