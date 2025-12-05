using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using ClinicaPsi.Web.Extensions;
using System.Security.Claims;
using System.Globalization;

namespace ClinicaPsi.Web.Pages.Psicologo
{
    [Authorize(Roles = "Admin,Psicologo")]
    public class AgendaModel : PageModel
    {
        private readonly AppDbContext _context;

        public AgendaModel(AppDbContext context)
        {
            _context = context;
            // Configurar cultura brasileira
            var culturaBrasileira = new CultureInfo("pt-BR");
            Thread.CurrentThread.CurrentCulture = culturaBrasileira;
            Thread.CurrentThread.CurrentUICulture = culturaBrasileira;
        }

        public DateTime SemanaAtual { get; set; } = DateTime.Today;
        public List<Consulta> ConsultasSemana { get; set; } = new();
        public List<Consulta> ProximasConsultas { get; set; } = new();
        public List<Paciente> PacientesDisponiveis { get; set; } = new();
        public decimal ValorConsultaPadrao { get; set; }

        public async Task<IActionResult> OnGetAsync(string? semana = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Forbid();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            
            int psicologoId;
            
            // Se for Admin, permitir acesso mas usar o primeiro psicólogo disponível
            if (User.IsInRole("Admin") && user?.PsicologoId == null)
            {
                var primeiroPsicologo = await _context.Psicologos.FirstOrDefaultAsync();
                if (primeiroPsicologo == null)
                    return NotFound("Nenhum psicólogo encontrado no sistema");
                
                psicologoId = primeiroPsicologo.Id;
                ValorConsultaPadrao = primeiroPsicologo.ValorConsulta;
            }
            else if (user?.PsicologoId == null)
            {
                return Forbid();
            }
            else
            {
                psicologoId = user.PsicologoId.Value;
            }

            // Buscar dados do psicólogo
            var psicologo = await _context.Psicologos.FindAsync(psicologoId);
            if (psicologo == null)
                return NotFound();

            ValorConsultaPadrao = psicologo.ValorConsulta;

            // Definir semana atual
            if (DateTime.TryParse(semana, out var semanaEscolhida))
            {
                SemanaAtual = semanaEscolhida.StartOfWeek(DayOfWeek.Monday);
            }
            else
            {
                SemanaAtual = DateTime.Today.StartOfWeek(DayOfWeek.Monday);
            }

            var inicioSemana = SemanaAtual;
            var fimSemana = SemanaAtual.AddDays(6);

            // Buscar consultas da semana
            ConsultasSemana = await _context.Consultas
                .Include(c => c.Paciente)
                .Where(c => c.PsicologoId == psicologoId &&
                           c.DataHorario.Date >= inicioSemana.Date &&
                           c.DataHorario.Date <= fimSemana.Date)
                .OrderBy(c => c.DataHorario)
                .ToListAsync();

            // Buscar próximas consultas (próximos 7 dias)
            ProximasConsultas = await _context.Consultas
                .Include(c => c.Paciente)
                .Where(c => c.PsicologoId == psicologoId &&
                           c.DataHorario >= DateTime.Now &&
                           c.DataHorario <= DateTime.Now.AddDays(7) &&
                           c.Status != StatusConsulta.Cancelada)
                .OrderBy(c => c.DataHorario)
                .ToListAsync();

            // Buscar pacientes disponíveis
            PacientesDisponiveis = await _context.Pacientes
                .Where(p => _context.Consultas
                    .Any(c => c.PacienteId == p.Id && c.PsicologoId == psicologoId))
                .Union(_context.Pacientes.Take(20)) // Incluir alguns pacientes gerais
                .Distinct()
                .OrderBy(p => p.Nome)
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostAgendarConsultaAsync(
            int pacienteId,
            string tipoConsulta,
            DateTime dataConsulta,
            string horaConsulta,
            int duracao,
            decimal valor,
            string? observacoes)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Forbid();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user?.PsicologoId == null)
                return Forbid();

            try
            {
                // Combinar data e hora
                if (!TimeSpan.TryParse(horaConsulta, out var hora))
                {
                    ModelState.AddModelError("", "Horário inválido");
                    return Page();
                }

                var dataHorario = dataConsulta.Date.Add(hora);

                // Verificar se já existe consulta no horário
                var consultaExistente = await _context.Consultas
                    .AnyAsync(c => c.PsicologoId == user.PsicologoId &&
                                  c.DataHorario == dataHorario &&
                                  c.Status != StatusConsulta.Cancelada);

                if (consultaExistente)
                {
                    ModelState.AddModelError("", "Já existe uma consulta agendada para este horário");
                    return await OnGetAsync();
                }

                // Criar nova consulta
                var novaConsulta = new Consulta
                {
                    PacienteId = pacienteId,
                    PsicologoId = user.PsicologoId.Value,
                    DataHorario = dataHorario,
                    DuracaoMinutos = duracao,
                    Valor = valor,
                    Status = StatusConsulta.Agendada,
                    Tipo = Enum.Parse<TipoConsulta>(tipoConsulta),
                    Observacoes = observacoes,
                    DataCriacao = DateTime.Now
                };

                _context.Consultas.Add(novaConsulta);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Consulta agendada com sucesso!";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Erro ao agendar consulta: " + ex.Message);
                return await OnGetAsync();
            }
        }
    }

    public static class DateTimeExtensions
    {
        public static DateTime StartOfWeek(this DateTime dt, DayOfWeek startOfWeek)
        {
            int diff = (7 + (dt.DayOfWeek - startOfWeek)) % 7;
            return dt.AddDays(-1 * diff).Date;
        }
    }
}