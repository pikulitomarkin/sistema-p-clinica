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
    public class RelatoriosModel : PageModel
    {
        private readonly AppDbContext _context;

        public RelatoriosModel(AppDbContext context)
        {
            _context = context;
        }

        // Filtros
        public DateTime DataInicio { get; set; } = DateTime.Today.AddDays(-30);
        public DateTime DataFim { get; set; } = DateTime.Today;

        // Estatísticas Gerais
        public int TotalConsultas { get; set; }
        public decimal ReceitaTotal { get; set; }
        public int PacientesAtendidos { get; set; }
        public double TaxaComparecimento { get; set; }

        // Variações (comparação com período anterior)
        public double VariacaoConsultas { get; set; }
        public double VariacaoReceita { get; set; }
        public double VariacaoPacientes { get; set; }
        public double VariacaoComparecimento { get; set; }

        // Status das Consultas
        public int ConsultasRealizadas { get; set; }
        public int ConsultasAgendadas { get; set; }
        public int ConsultasCanceladas { get; set; }
        public int NoShows { get; set; }
        public int ConsultasGratuitas { get; set; }

        // Dados para Gráficos
        public List<ConsultasPorDiaDto> ConsultasPorDia { get; set; } = new();
        public List<ReceitaPorMesDto> ReceitaPorMes { get; set; } = new();
        public List<ConsultasPorHorarioDto> ConsultasPorHorario { get; set; } = new();
        public List<ConsultasPorDiaSemanaDto> ConsultasPorDiaSemana { get; set; } = new();

        // Rankings
        public List<TopPacienteDto> TopPacientes { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(DateTime? dataInicio = null, DateTime? dataFim = null)
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

                // Definir período
                if (dataInicio.HasValue && dataFim.HasValue)
                {
                    DataInicio = dataInicio.Value;
                    DataFim = dataFim.Value;
                }

                // Garantir que a data fim seja o final do dia
                DataFim = DataFim.Date.AddDays(1).AddTicks(-1);

                await CalcularEstatisticasGeraisAsync(psicologoId);
                await CalcularVariacoesAsync(psicologoId);
                await CalcularStatusConsultasAsync(psicologoId);
                await GerarDadosGraficosAsync(psicologoId);
                await GerarRankingsAsync(psicologoId);

                return Page();
            }
            catch (Exception ex)
            {
                TempData["Error"] = "A página está sendo atualizada. Por favor, aguarde alguns minutos e recarregue.";
                return Page();
            }
        }

        private async Task CalcularEstatisticasGeraisAsync(int psicologoId)
        {
            var consultas = await _context.Consultas
                .Include(c => c.Paciente)
                .Where(c => c.PsicologoId == psicologoId &&
                           c.DataHorario >= DataInicio &&
                           c.DataHorario <= DataFim)
                .ToListAsync();

            TotalConsultas = consultas.Count;
            ReceitaTotal = consultas.Where(c => c.Status == StatusConsulta.Realizada).Sum(c => c.Valor);
            PacientesAtendidos = consultas.Select(c => c.PacienteId).Distinct().Count();

            var consultasComparenciaPossivel = consultas.Where(c => 
                c.Status == StatusConsulta.Realizada || 
                c.Status == StatusConsulta.NoShow ||
                c.Status == StatusConsulta.Cancelada).Count();

            TaxaComparecimento = consultasComparenciaPossivel > 0 
                ? (double)consultas.Count(c => c.Status == StatusConsulta.Realizada) / consultasComparenciaPossivel * 100
                : 0;
        }

        private async Task CalcularVariacoesAsync(int psicologoId)
        {
            var diasPeriodo = (DataFim.Date - DataInicio.Date).Days + 1;
            var dataInicioAnterior = DataInicio.AddDays(-diasPeriodo);
            var dataFimAnterior = DataInicio.AddTicks(-1);

            var consultasAnterior = await _context.Consultas
                .Where(c => c.PsicologoId == psicologoId &&
                           c.DataHorario >= dataInicioAnterior &&
                           c.DataHorario <= dataFimAnterior)
                .ToListAsync();

            var totalConsultasAnterior = consultasAnterior.Count;
            var receitaAnterior = consultasAnterior.Where(c => c.Status == StatusConsulta.Realizada).Sum(c => c.Valor);
            var pacientesAnterior = consultasAnterior.Select(c => c.PacienteId).Distinct().Count();

            var consultasComparenciaPossivelAnterior = consultasAnterior.Where(c => 
                c.Status == StatusConsulta.Realizada || 
                c.Status == StatusConsulta.NoShow ||
                c.Status == StatusConsulta.Cancelada).Count();

            var taxaComparecimentoAnterior = consultasComparenciaPossivelAnterior > 0 
                ? (double)consultasAnterior.Count(c => c.Status == StatusConsulta.Realizada) / consultasComparenciaPossivelAnterior * 100
                : 0;

            // Calcular variações percentuais
            VariacaoConsultas = totalConsultasAnterior > 0 
                ? Math.Round(((double)(TotalConsultas - totalConsultasAnterior) / totalConsultasAnterior) * 100, 1)
                : 0;

            VariacaoReceita = receitaAnterior > 0 
                ? Math.Round(((double)(ReceitaTotal - receitaAnterior) / (double)receitaAnterior) * 100, 1)
                : 0;

            VariacaoPacientes = pacientesAnterior > 0 
                ? Math.Round(((double)(PacientesAtendidos - pacientesAnterior) / pacientesAnterior) * 100, 1)
                : 0;

            VariacaoComparecimento = taxaComparecimentoAnterior > 0 
                ? Math.Round(TaxaComparecimento - taxaComparecimentoAnterior, 1)
                : 0;
        }

        private async Task CalcularStatusConsultasAsync(int psicologoId)
        {
            var statusCounts = await _context.Consultas
                .Where(c => c.PsicologoId == psicologoId &&
                           c.DataHorario >= DataInicio &&
                           c.DataHorario <= DataFim)
                .GroupBy(c => c.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            ConsultasRealizadas = statusCounts.FirstOrDefault(s => s.Status == StatusConsulta.Realizada)?.Count ?? 0;
            ConsultasAgendadas = statusCounts.FirstOrDefault(s => s.Status == StatusConsulta.Agendada)?.Count ?? 0;
            ConsultasCanceladas = statusCounts.FirstOrDefault(s => s.Status == StatusConsulta.Cancelada)?.Count ?? 0;
            NoShows = statusCounts.FirstOrDefault(s => s.Status == StatusConsulta.NoShow)?.Count ?? 0;

            ConsultasGratuitas = await _context.Consultas
                .Where(c => c.PsicologoId == psicologoId &&
                           c.DataHorario >= DataInicio &&
                           c.DataHorario <= DataFim &&
                           c.Tipo == TipoConsulta.Gratuita)
                .CountAsync();
        }

        private async Task GerarDadosGraficosAsync(int psicologoId)
        {
            // Consultas por dia
            ConsultasPorDia = await _context.Consultas
                .Where(c => c.PsicologoId == psicologoId &&
                           c.DataHorario >= DataInicio &&
                           c.DataHorario <= DataFim)
                .GroupBy(c => c.DataHorario.Date)
                .Select(g => new ConsultasPorDiaDto
                {
                    Data = g.Key,
                    Total = g.Count()
                })
                .OrderBy(x => x.Data)
                .ToListAsync();

            // Receita por mês
            ReceitaPorMes = await _context.Consultas
                .Where(c => c.PsicologoId == psicologoId &&
                           c.DataHorario >= DataInicio &&
                           c.DataHorario <= DataFim &&
                           c.Status == StatusConsulta.Realizada)
                .GroupBy(c => new { c.DataHorario.Year, c.DataHorario.Month })
                .Select(g => new ReceitaPorMesDto
                {
                    Mes = $"{g.Key.Month:00}/{g.Key.Year}",
                    Valor = g.Sum(c => c.Valor)
                })
                .ToListAsync();

            // Consultas por horário
            ConsultasPorHorario = await _context.Consultas
                .Where(c => c.PsicologoId == psicologoId &&
                           c.DataHorario >= DataInicio &&
                           c.DataHorario <= DataFim)
                .GroupBy(c => c.DataHorario.Hour)
                .Select(g => new ConsultasPorHorarioDto
                {
                    Horario = g.Key,
                    Total = g.Count()
                })
                .OrderBy(x => x.Horario)
                .ToListAsync();

            // Consultas por dia da semana
            var consultasPorDiaSemana = await _context.Consultas
                .Where(c => c.PsicologoId == psicologoId &&
                           c.DataHorario >= DataInicio &&
                           c.DataHorario <= DataFim)
                .GroupBy(c => c.DataHorario.DayOfWeek)
                .Select(g => new { 
                    DiaSemana = g.Key, 
                    TotalConsultas = g.Count(),
                    ReceitaTotal = g.Where(x => x.Status == StatusConsulta.Realizada).Sum(x => x.Valor)
                })
                .ToListAsync();

            ConsultasPorDiaSemana = consultasPorDiaSemana.Select(x => new ConsultasPorDiaSemanaDto
            {
                DiaSemana = ObterNomeDiaSemana(x.DiaSemana),
                TotalConsultas = x.TotalConsultas,
                ReceitaTotal = x.ReceitaTotal
            }).ToList();
        }

        private async Task GerarRankingsAsync(int psicologoId)
        {
            TopPacientes = await _context.Consultas
                .Include(c => c.Paciente)
                .Where(c => c.PsicologoId == psicologoId &&
                           c.DataHorario >= DataInicio &&
                           c.DataHorario <= DataFim)
                .GroupBy(c => new { c.PacienteId, c.Paciente!.Nome })
                .Select(g => new TopPacienteDto
                {
                    PacienteId = g.Key.PacienteId,
                    NomePaciente = g.Key.Nome,
                    TotalConsultas = g.Count(),
                    ReceitaTotal = g.Where(x => x.Status == StatusConsulta.Realizada).Sum(x => x.Valor)
                })
                .OrderByDescending(x => x.TotalConsultas)
                .Take(10)
                .ToListAsync();
        }

        private static string ObterNomeDiaSemana(DayOfWeek diaSemana)
        {
            var cultura = new CultureInfo("pt-BR");
            return cultura.DateTimeFormat.GetDayName(diaSemana);
        }
    }

    public class ConsultasPorDiaDto
    {
        public DateTime Data { get; set; }
        public int Total { get; set; }
    }

    public class ReceitaPorMesDto
    {
        public string Mes { get; set; } = string.Empty;
        public decimal Valor { get; set; }
    }

    public class ConsultasPorHorarioDto
    {
        public int Horario { get; set; }
        public int Total { get; set; }
    }

    public class ConsultasPorDiaSemanaDto
    {
        public string DiaSemana { get; set; } = string.Empty;
        public int TotalConsultas { get; set; }
        public decimal ReceitaTotal { get; set; }
    }

    public class TopPacienteDto
    {
        public int PacienteId { get; set; }
        public string NomePaciente { get; set; } = string.Empty;
        public int TotalConsultas { get; set; }
        public decimal ReceitaTotal { get; set; }
    }
}