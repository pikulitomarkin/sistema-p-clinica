using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using System.Security.Claims;
using System.Globalization;

namespace ClinicaPsi.Web.Pages.Psicologo
{
    [Authorize(Roles = "Admin,Psicologo")]
    public class AgendaDiariaModel : PageModel
    {
        private readonly AppDbContext _context;

        public AgendaDiariaModel(AppDbContext context)
        {
            _context = context;
            // Configurar cultura brasileira
            var culturaBrasileira = new CultureInfo("pt-BR");
            Thread.CurrentThread.CurrentCulture = culturaBrasileira;
            Thread.CurrentThread.CurrentUICulture = culturaBrasileira;
        }

        public DateTime DataSelecionada { get; set; } = DateTime.Today;
        public List<Consulta> ConsultasDia { get; set; } = new();
        public int PsicologoId { get; set; }

        public async Task<IActionResult> OnGetAsync(string? data)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                    return Forbid();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            
            // Se for Admin, permitir acesso mas usar o primeiro psicólogo disponível
            if (User.IsInRole("Admin") && user?.PsicologoId == null)
            {
                var primeiroPsicologo = await _context.Psicologos.FirstOrDefaultAsync();
                if (primeiroPsicologo == null)
                    return NotFound("Nenhum psicólogo encontrado no sistema");
                
                PsicologoId = primeiroPsicologo.Id;
            }
            else if (user?.PsicologoId == null)
            {
                return Forbid();
            }
            else
            {
                PsicologoId = user.PsicologoId.Value;
            }

            // Definir data selecionada
            if (!string.IsNullOrEmpty(data) && DateTime.TryParse(data, out var dataEscolhida))
            {
                DataSelecionada = dataEscolhida.Date;
            }

            // Buscar consultas do dia
            ConsultasDia = await _context.Consultas
                .Include(c => c.Paciente)
                .Where(c => c.PsicologoId == PsicologoId &&
                           c.DataHorario.Date == DataSelecionada.Date)
                .OrderBy(c => c.DataHorario)
                .ToListAsync();

                return Page();
            }
            catch (Exception)
            {
                TempData["Error"] = "A página está sendo atualizada. Por favor, aguarde alguns minutos e recarregue.";
                return Page();
            }
        }
    }
}
