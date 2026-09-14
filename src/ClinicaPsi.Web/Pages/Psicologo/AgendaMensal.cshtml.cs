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
    public class AgendaMensalModel : PageModel
    {
        private readonly AppDbContext _context;

        public AgendaMensalModel(AppDbContext context)
        {
            _context = context;
            // Configurar cultura brasileira
            var culturaBrasileira = new CultureInfo("pt-BR");
            Thread.CurrentThread.CurrentCulture = culturaBrasileira;
            Thread.CurrentThread.CurrentUICulture = culturaBrasileira;
        }

        public DateTime MesSelecionado { get; set; } = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        public List<Consulta> ConsultasMes { get; set; } = new();
        public int PsicologoId { get; set; }
        
        // Estatísticas
        public int TotalConsultasMes { get; set; }
        public int ConsultasRealizadas { get; set; }
        public int ConsultasAgendadas { get; set; }
        public int ConsultasCanceladas { get; set; }

        public async Task<IActionResult> OnGetAsync(int? ano, int? mes)
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

            // Definir mês selecionado
            if (ano.HasValue && mes.HasValue && mes.Value >= 1 && mes.Value <= 12)
            {
                MesSelecionado = new DateTime(ano.Value, mes.Value, 1);
            }

            var inicioMes = MesSelecionado;
            var fimMes = MesSelecionado.AddMonths(1).AddDays(-1);

            var agora = DateTime.Now;

            // Todas do mês (para estatísticas / histórico no calendário mensal)
            var todasDoMes = await _context.Consultas
                .Include(c => c.Paciente)
                .Where(c => c.PsicologoId == PsicologoId &&
                           c.DataHorario.Date >= inicioMes.Date &&
                           c.DataHorario.Date <= fimMes.Date)
                .OrderBy(c => c.DataHorario)
                .ToListAsync();

            // Grade mensal: só ativas ainda não encerradas
            ConsultasMes = todasDoMes
                .Where(c => c.Status != StatusConsulta.Cancelada &&
                            c.Status != StatusConsulta.Realizada &&
                            c.Status != StatusConsulta.NoShow &&
                            c.DataHorario.AddMinutes(c.DuracaoMinutos) >= agora)
                .ToList();

            // Calcular estatísticas com o mês completo
            TotalConsultasMes = todasDoMes.Count;
            ConsultasRealizadas = todasDoMes.Count(c => c.Status == StatusConsulta.Realizada);
            ConsultasAgendadas = todasDoMes.Count(c =>
                c.Status == StatusConsulta.Agendada ||
                c.Status == StatusConsulta.Confirmada);
            ConsultasCanceladas = todasDoMes.Count(c => c.Status == StatusConsulta.Cancelada);

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
