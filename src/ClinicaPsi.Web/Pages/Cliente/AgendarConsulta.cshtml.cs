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
    public class AgendarConsultaModel : PageModel
    {
        private readonly AppDbContext _context;

        public AgendarConsultaModel(AppDbContext context)
        {
            _context = context;
        }

        public List<ClinicaPsi.Shared.Models.Psicologo> Psicologos { get; set; } = new();
        public List<Consulta> ConsultasExistentes { get; set; } = new();
        public Paciente? PacienteAtual { get; set; }
        public DateTime DataSelecionada { get; set; } = DateTime.Today;

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            public int PsicologoId { get; set; }
            public DateTime DataHorario { get; set; }
            public int DuracaoMinutos { get; set; } = 50;
            public TipoConsulta Tipo { get; set; } = TipoConsulta.Normal;
            public FormatoConsulta Formato { get; set; } = FormatoConsulta.Presencial;
            public string? Observacoes { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(DateTime? data)
        {
            try
            {
                if (data.HasValue)
                    DataSelecionada = data.Value;

                await CarregarDadosAsync();
                return Page();
            }
            catch (Exception ex)
            {
                TempData["Error"] = "A página está sendo atualizada. Por favor, aguarde alguns minutos e recarregue.";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await CarregarDadosAsync();
                return Page();
            }

            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user?.PacienteId == null)
                {
                    ModelState.AddModelError("", "Usuário não está associado a um paciente.");
                    await CarregarDadosAsync();
                    return Page();
                }

                // Verificar se já existe consulta no mesmo horário
                var consultaExistente = await _context.Consultas
                    .Where(c => c.PsicologoId == Input.PsicologoId &&
                               c.DataHorario == Input.DataHorario &&
                               c.Status != StatusConsulta.Cancelada)
                    .FirstOrDefaultAsync();

                if (consultaExistente != null)
                {
                    ModelState.AddModelError("", "Este horário já está ocupado. Selecione outro horário.");
                    await CarregarDadosAsync();
                    return Page();
                }

                // Verificar se o paciente tem consultas gratuitas disponíveis
                var paciente = await _context.Pacientes.FindAsync(user.PacienteId.Value);
                if (paciente == null)
                {
                    ModelState.AddModelError("", "Paciente não encontrado.");
                    await CarregarDadosAsync();
                    return Page();
                }

                var psicologo = await _context.Psicologos.FindAsync(Input.PsicologoId);
                if (psicologo == null)
                {
                    ModelState.AddModelError("", "Psicólogo não encontrado.");
                    await CarregarDadosAsync();
                    return Page();
                }

                // Determinar o tipo e valor da consulta
                var tipoConsulta = Input.Tipo;
                var valorConsulta = psicologo.ValorConsulta;

                if (paciente.ConsultasGratuitas > 0 && Input.Tipo == TipoConsulta.Gratuita)
                {
                    valorConsulta = 0;
                    tipoConsulta = TipoConsulta.Gratuita;
                }

                // Criar nova consulta
                var consulta = new Consulta
                {
                    PacienteId = user.PacienteId.Value,
                    PsicologoId = Input.PsicologoId,
                    DataHorario = Input.DataHorario,
                    DuracaoMinutos = Input.DuracaoMinutos,
                    Valor = valorConsulta,
                    Status = StatusConsulta.Agendada,
                    Tipo = tipoConsulta,
                    Observacoes = Input.Observacoes,
                    DataAgendamento = DateTime.Now,
                    DataCriacao = DateTime.Now,
                    NotificacaoEnviada = false,
                    ConfirmacaoRecebida = false
                };

                _context.Consultas.Add(consulta);

                // Se for consulta gratuita, decrementar do paciente
                if (tipoConsulta == TipoConsulta.Gratuita)
                {
                    paciente.ConsultasGratuitas--;
                    _context.Pacientes.Update(paciente);
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = "Consulta agendada com sucesso!";
                return RedirectToPage("MinhasConsultas");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Erro ao agendar consulta: " + ex.Message);
                await CarregarDadosAsync();
                return Page();
            }
        }

        public async Task<IActionResult> OnGetHorariosDisponiveisAsync(int psicologoId, DateTime data)
        {
            var psicologo = await _context.Psicologos.FindAsync(psicologoId);
            if (psicologo == null)
                return NotFound();

            var consultasOcupadas = await _context.Consultas
                .Where(c => c.PsicologoId == psicologoId &&
                           c.DataHorario.Date == data.Date &&
                           c.Status != StatusConsulta.Cancelada)
                .Select(c => c.DataHorario)
                .ToListAsync();

            var horariosDisponiveis = GerarHorariosDisponiveis(psicologo, data, consultasOcupadas);

            return new JsonResult(horariosDisponiveis.Select(h => new {
                valor = h.ToString("yyyy-MM-ddTHH:mm"),
                texto = h.ToString("HH:mm")
            }));
        }

        private async Task CarregarDadosAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user?.PacienteId != null)
            {
                PacienteAtual = await _context.Pacientes
                    .FindAsync(user.PacienteId.Value);
            }

            Psicologos = await _context.Psicologos
                .Where(p => p.Ativo)
                .OrderBy(p => p.Nome)
                .ToListAsync();

            ConsultasExistentes = await _context.Consultas
                .Include(c => c.Psicologo)
                .Where(c => c.DataHorario.Date == DataSelecionada.Date &&
                           c.Status != StatusConsulta.Cancelada)
                .OrderBy(c => c.DataHorario)
                .ToListAsync();
        }

        private List<DateTime> GerarHorariosDisponiveis(ClinicaPsi.Shared.Models.Psicologo psicologo, DateTime data, List<DateTime> horariosOcupados)
        {
            var horarios = new List<DateTime>();
            var diaSemana = data.DayOfWeek;

            // Verificar se o psicólogo atende no dia da semana
            bool atendeNoDia = diaSemana switch
            {
                DayOfWeek.Monday => psicologo.AtendeSegunda,
                DayOfWeek.Tuesday => psicologo.AtendeTerca,
                DayOfWeek.Wednesday => psicologo.AtendeQuarta,
                DayOfWeek.Thursday => psicologo.AtendeQuinta,
                DayOfWeek.Friday => psicologo.AtendeSexta,
                DayOfWeek.Saturday => psicologo.AtendeSabado,
                DayOfWeek.Sunday => psicologo.AtendeDomingo,
                _ => false
            };

            if (!atendeNoDia) return horarios;

            // Gerar horários da manhã
            if (psicologo.AtendeManha)
            {
                var inicioManha = data.Date.Add(psicologo.HorarioInicioManha);
                var fimManha = data.Date.Add(psicologo.HorarioFimManha);

                for (var hora = inicioManha; hora < fimManha; hora = hora.AddMinutes(50))
                {
                    if (!horariosOcupados.Contains(hora) && hora > DateTime.Now)
                    {
                        horarios.Add(hora);
                    }
                }
            }

            // Gerar horários da tarde
            if (psicologo.AtendeTarde)
            {
                var inicioTarde = data.Date.Add(psicologo.HorarioInicioTarde);
                var fimTarde = data.Date.Add(psicologo.HorarioFimTarde);

                for (var hora = inicioTarde; hora < fimTarde; hora = hora.AddMinutes(50))
                {
                    if (!horariosOcupados.Contains(hora) && hora > DateTime.Now)
                    {
                        horarios.Add(hora);
                    }
                }
            }

            return horarios;
        }
    }
}
