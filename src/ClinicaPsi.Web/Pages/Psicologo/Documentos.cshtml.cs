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

        public List<Consulta> ConsultasRealizadas { get; set; } = new();
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

                // Buscar consultas realizadas dos últimos 90 dias
                var dataLimite = DateTime.Now.AddDays(-90);
                ConsultasRealizadas = await _context.Consultas
                    .Include(c => c.Paciente)
                    .Include(c => c.Psicologo)
                    .Where(c => c.PsicologoId == psicologo.Id 
                        && c.Status == StatusConsulta.Realizada
                        && c.DataHorario >= dataLimite)
                    .OrderByDescending(c => c.DataHorario)
                    .ToListAsync();

                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Erro ao carregar consultas: {ex.Message}";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostGerarDeclaracaoAsync(int consultaId)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return RedirectToPage("/Account/Login");
                }

                // Verificar se a consulta pertence ao psicólogo logado
                var psicologo = await _context.Psicologos
                    .FirstOrDefaultAsync(p => p.UserId == user.Id);

                if (psicologo == null)
                {
                    ErrorMessage = "Psicólogo não encontrado.";
                    return await OnGetAsync();
                }

                var consulta = await _context.Consultas
                    .Include(c => c.Paciente)
                    .FirstOrDefaultAsync(c => c.Id == consultaId && c.PsicologoId == psicologo.Id);

                if (consulta == null)
                {
                    ErrorMessage = "Consulta não encontrada ou você não tem permissão para gerar este documento.";
                    return await OnGetAsync();
                }

                if (consulta.Status != StatusConsulta.Realizada)
                {
                    ErrorMessage = "Só é possível gerar declaração para consultas realizadas.";
                    return await OnGetAsync();
                }

                // Gerar PDF
                var pdfBytes = await _pdfService.GerarDeclaracaoComparecimentoAsync(consultaId);
                
                var nomeArquivo = $"Declaracao_{consulta.Paciente?.Nome?.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.pdf";
                
                return File(pdfBytes, "application/pdf", nomeArquivo);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Erro ao gerar declaração: {ex.Message}";
                return await OnGetAsync();
            }
        }

        public async Task<IActionResult> OnPostGerarAtestadoAsync(
            int consultaId, 
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

                // Verificar se a consulta pertence ao psicólogo logado
                var psicologo = await _context.Psicologos
                    .FirstOrDefaultAsync(p => p.UserId == user.Id);

                if (psicologo == null)
                {
                    ErrorMessage = "Psicólogo não encontrado.";
                    return await OnGetAsync();
                }

                var consulta = await _context.Consultas
                    .Include(c => c.Paciente)
                    .FirstOrDefaultAsync(c => c.Id == consultaId && c.PsicologoId == psicologo.Id);

                if (consulta == null)
                {
                    ErrorMessage = "Consulta não encontrada ou você não tem permissão para gerar este documento.";
                    return await OnGetAsync();
                }

                if (consulta.Status != StatusConsulta.Realizada)
                {
                    ErrorMessage = "Só é possível gerar atestado para consultas realizadas.";
                    return await OnGetAsync();
                }

                if (diasAfastamento < 1 || diasAfastamento > 90)
                {
                    ErrorMessage = "O período de afastamento deve ser entre 1 e 90 dias.";
                    return await OnGetAsync();
                }

                // Gerar PDF
                var pdfBytes = await _pdfService.GerarAtestadoAsync(
                    consultaId, 
                    cid ?? string.Empty, 
                    diasAfastamento, 
                    observacoes ?? string.Empty);
                
                var nomeArquivo = $"Atestado_{consulta.Paciente?.Nome?.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.pdf";
                
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
