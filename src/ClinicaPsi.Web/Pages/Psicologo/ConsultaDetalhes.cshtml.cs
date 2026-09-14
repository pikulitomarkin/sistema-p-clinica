using System.Security.Claims;
using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ClinicaPsi.Web.Pages.Psicologo
{
    [Authorize(Roles = "Admin,Psicologo")]
    public class ConsultaDetalhesModel : PageModel
    {
        private readonly AppDbContext _context;

        public ConsultaDetalhesModel(AppDbContext context)
        {
            _context = context;
        }

        public Consulta Consulta { get; set; } = null!;
        public int? ProntuarioId { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var consulta = await _context.Consultas
                .Include(c => c.Paciente)
                .Include(c => c.Psicologo)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (consulta == null)
            {
                TempData["Error"] = "Consulta não encontrada.";
                return RedirectToPage("/Psicologo/Consultas");
            }

            var acesso = await VerificarAcessoConsultaAsync(consulta);
            if (acesso != null)
                return acesso;

            Consulta = consulta;

            ProntuarioId = await _context.ProntuariosEletronicos
                .Where(p => p.ConsultaId == id)
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync();

            return Page();
        }

        private async Task<IActionResult?> VerificarAcessoConsultaAsync(Consulta consulta)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Forbid();

            if (User.IsInRole("Admin"))
                return null;

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user?.PsicologoId == null)
                return Forbid();

            if (consulta.PsicologoId != user.PsicologoId.Value)
            {
                TempData["Error"] = "Você não tem permissão para ver esta consulta.";
                return RedirectToPage("/Psicologo/Consultas");
            }

            return null;
        }
    }
}
