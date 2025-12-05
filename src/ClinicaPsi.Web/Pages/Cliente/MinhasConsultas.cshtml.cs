using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using ClinicaPsi.Web.Extensions;
using System.Security.Claims;

namespace ClinicaPsi.Web.Pages.Cliente
{
    [Authorize]
    public class MinhasConsultasModel : PageModel
    {
        private readonly AppDbContext _context;

        public MinhasConsultasModel(AppDbContext context)
        {
            _context = context;
        }

        public List<Consulta> ConsultasProximas { get; set; } = new();
        public List<Consulta> ConsultasRealizadas { get; set; } = new();
        public List<Consulta> ConsultasCanceladas { get; set; } = new();
        public Paciente? PacienteAtual { get; set; }
        public string FiltroStatus { get; set; } = "proximas";
        public string Busca { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync(string filtroStatus = "proximas", string busca = "")
        {
            try
            {
                FiltroStatus = filtroStatus;
                Busca = busca;

                await CarregarDadosAsync();
                return Page();
            }
            catch (Exception ex)
            {
                TempData["Error"] = "A página está sendo atualizada. Por favor, aguarde alguns minutos e recarregue.";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostCancelarConsultaAsync(int consultaId, string motivo = "")
        {
            try
            {
                var consulta = await _context.Consultas
                    .Include(c => c.Paciente)
                    .FirstOrDefaultAsync(c => c.Id == consultaId);

                if (consulta == null)
                {
                    TempData["Error"] = "Consulta não encontrada.";
                    return RedirectToPage();
                }

                // Verificar se a consulta pode ser cancelada (24h antes)
                var horasParaConsulta = (consulta.DataHorario - DateTime.Now).TotalHours;
                if (horasParaConsulta < 24)
                {
                    TempData["Error"] = "Consultas só podem ser canceladas com pelo menos 24 horas de antecedência.";
                    return RedirectToPage();
                }

                consulta.Status = StatusConsulta.Cancelada;
                consulta.DataCancelamento = DateTime.Now;
                consulta.MotivoCancelamento = string.IsNullOrWhiteSpace(motivo) ? "Cancelado pelo paciente" : motivo;
                consulta.DataAtualizacao = DateTime.Now;

                await _context.SaveChangesAsync();

                TempData["Success"] = "Consulta cancelada com sucesso.";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Erro ao cancelar consulta: " + ex.Message;
                return RedirectToPage();
            }
        }

        public async Task<IActionResult> OnPostReagendarConsultaAsync(int consultaId)
        {
            try
            {
                var consulta = await _context.Consultas.FindAsync(consultaId);
                if (consulta == null)
                {
                    TempData["Error"] = "Consulta não encontrada.";
                    return RedirectToPage();
                }

                consulta.Status = StatusConsulta.Reagendada;
                consulta.DataAtualizacao = DateTime.Now;
                await _context.SaveChangesAsync();

                TempData["Success"] = "Consulta marcada para reagendamento. Entre em contato para escolher nova data.";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Erro ao reagendar consulta: " + ex.Message;
                return RedirectToPage();
            }
        }

        public async Task<IActionResult> OnPostConfirmarPresencaAsync(int consultaId)
        {
            try
            {
                var consulta = await _context.Consultas.FindAsync(consultaId);
                if (consulta == null)
                {
                    TempData["Error"] = "Consulta não encontrada.";
                    return RedirectToPage();
                }

                consulta.ConfirmacaoRecebida = true;
                consulta.DataAtualizacao = DateTime.Now;
                await _context.SaveChangesAsync();

                TempData["Success"] = "Presença confirmada com sucesso!";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Erro ao confirmar presença: " + ex.Message;
                return RedirectToPage();
            }
        }

        private async Task CarregarDadosAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user?.PacienteId == null) return;

            PacienteAtual = await _context.Pacientes
                .FindAsync(user.PacienteId.Value);

            var consultasQuery = _context.Consultas
                .Include(c => c.Psicologo)
                .Where(c => c.PacienteId == user.PacienteId.Value);

            // Aplicar filtro de busca
            if (!string.IsNullOrWhiteSpace(Busca))
            {
                consultasQuery = consultasQuery.Where(c => 
                    c.Psicologo.Nome.Contains(Busca) ||
                    c.Observacoes!.Contains(Busca));
            }

            var todasConsultas = await consultasQuery
                .OrderBy(c => c.DataHorario)
                .ToListAsync();

            var agora = DateTime.Now;

            ConsultasProximas = todasConsultas
                .Where(c => c.DataHorario >= agora && 
                           (c.Status == StatusConsulta.Agendada || c.Status == StatusConsulta.Confirmada))
                .OrderBy(c => c.DataHorario)
                .ToList();

            ConsultasRealizadas = todasConsultas
                .Where(c => c.Status == StatusConsulta.Realizada)
                .OrderByDescending(c => c.DataHorario)
                .ToList();

            ConsultasCanceladas = todasConsultas
                .Where(c => c.Status == StatusConsulta.Cancelada || c.Status == StatusConsulta.NoShow)
                .OrderByDescending(c => c.DataHorario)
                .ToList();
        }

        public string GetStatusBadgeClass(StatusConsulta status)
        {
            return status switch
            {
                StatusConsulta.Agendada => "bg-primary",
                StatusConsulta.Confirmada => "bg-success",
                StatusConsulta.Realizada => "bg-info",
                StatusConsulta.Cancelada => "bg-danger",
                StatusConsulta.NoShow => "bg-warning",
                StatusConsulta.Reagendada => "bg-secondary",
                _ => "bg-secondary"
            };
        }

        public bool PodeSerCancelada(Consulta consulta)
        {
            var horasParaConsulta = (consulta.DataHorario - DateTime.Now).TotalHours;
            return horasParaConsulta >= 24 && 
                   (consulta.Status == StatusConsulta.Agendada || consulta.Status == StatusConsulta.Confirmada);
        }

        public bool PrecisaConfirmacao(Consulta consulta)
        {
            var horasParaConsulta = (consulta.DataHorario - DateTime.Now).TotalHours;
            return horasParaConsulta <= 48 && horasParaConsulta > 0 && 
                   !consulta.ConfirmacaoRecebida &&
                   (consulta.Status == StatusConsulta.Agendada || consulta.Status == StatusConsulta.Confirmada);
        }
    }
}