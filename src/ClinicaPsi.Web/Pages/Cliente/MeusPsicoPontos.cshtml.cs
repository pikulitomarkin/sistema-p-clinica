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
    public class MeusPsicoPontosModel : PageModel
    {
        private readonly AppDbContext _context;

        public MeusPsicoPontosModel(AppDbContext context)
        {
            _context = context;
        }

        public Paciente? PacienteAtual { get; set; }
        public List<HistoricoPontos> HistoricoCompleto { get; set; } = new();
        public int TotalPontos { get; set; }
        public int PontosParaProximaGratuita { get; set; }
        public int ConsultasGratuitasDisponiveis { get; set; }
        public int TotalConsultasRealizadas { get; set; }
        public int TotalPontosGanhos { get; set; }
        public int TotalPontosUsados { get; set; }
        public List<ConsultaGratuita> ConsultasGratuitasHistorico { get; set; } = new();

        // Ranking e Estatísticas
        public int PosicaoRanking { get; set; }
        public int TotalPacientes { get; set; }
        public List<RankingPaciente> TopPacientes { get; set; } = new();

        public class ConsultaGratuita
        {
            public DateTime DataConsulta { get; set; }
            public string NomePsicologo { get; set; } = string.Empty;
            public string EspecialidadesPsicologo { get; set; } = string.Empty;
            public int PontosUsados { get; set; }
        }

        public class RankingPaciente
        {
            public string Nome { get; set; } = string.Empty;
            public int PsicoPontos { get; set; }
            public int ConsultasRealizadas { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                await CarregarDadosAsync();
                return Page();
            }
            catch (Exception ex)
            {
                TempData["Error"] = "A página está sendo atualizada. Por favor, aguarde alguns minutos e recarregue.";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostUsarPontosAsync()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user?.PacienteId == null)
                {
                    TempData["Error"] = "Usuário não encontrado.";
                    return RedirectToPage();
                }

                var paciente = await _context.Pacientes.FindAsync(user.PacienteId.Value);
                if (paciente == null)
                {
                    TempData["Error"] = "Paciente não encontrado.";
                    return RedirectToPage();
                }

                if (paciente.PsicoPontos < 10)
                {
                    TempData["Error"] = "Você precisa de pelo menos 10 pontos para resgatar uma consulta gratuita.";
                    return RedirectToPage();
                }

                // Deduzir pontos e adicionar consulta gratuita
                paciente.PsicoPontos -= 10;
                paciente.ConsultasGratuitas += 1;

                // Registrar movimentação de pontos
                var historicoPontos = new HistoricoPontos
                {
                    PacienteId = paciente.Id,
                    TipoMovimentacao = TipoMovimentacaoPontos.Uso,
                    Pontos = 10,
                    Descricao = "Resgate de consulta gratuita",
                    DataMovimentacao = DateTime.Now
                };

                _context.HistoricoPontos.Add(historicoPontos);
                _context.Pacientes.Update(paciente);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Parabéns! Você ganhou uma consulta gratuita! Use na hora de agendar.";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Erro ao resgatar pontos: " + ex.Message;
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

            if (PacienteAtual == null) return;

            TotalPontos = PacienteAtual.PsicoPontos;
            ConsultasGratuitasDisponiveis = PacienteAtual.ConsultasGratuitas;
            TotalConsultasRealizadas = PacienteAtual.ConsultasRealizadas;

            // Calcular pontos para próxima gratuita
            PontosParaProximaGratuita = 10 - (TotalPontos % 10);
            if (PontosParaProximaGratuita == 10 && TotalPontos > 0)
                PontosParaProximaGratuita = 0;

            // Carregar histórico completo
            HistoricoCompleto = await _context.HistoricoPontos
                .Where(h => h.PacienteId == user.PacienteId.Value)
                .OrderByDescending(h => h.DataMovimentacao)
                .Take(50)
                .ToListAsync();

            // Calcular totais
            TotalPontosGanhos = HistoricoCompleto
                .Where(h => h.TipoMovimentacao == TipoMovimentacaoPontos.Ganho || 
                           h.TipoMovimentacao == TipoMovimentacaoPontos.Bonus)
                .Sum(h => h.Pontos);

            TotalPontosUsados = HistoricoCompleto
                .Where(h => h.TipoMovimentacao == TipoMovimentacaoPontos.Uso)
                .Sum(h => h.Pontos);

            // Carregar consultas gratuitas utilizadas
            ConsultasGratuitasHistorico = await _context.Consultas
                .Include(c => c.Psicologo)
                .Where(c => c.PacienteId == user.PacienteId.Value && 
                           c.Tipo == TipoConsulta.Gratuita &&
                           c.Status == StatusConsulta.Realizada)
                .OrderByDescending(c => c.DataHorario)
                .Take(10)
                .Select(c => new ConsultaGratuita
                {
                    DataConsulta = c.DataHorario,
                    NomePsicologo = c.Psicologo.Nome,
                    EspecialidadesPsicologo = c.Psicologo.Especialidades,
                    PontosUsados = 10
                })
                .ToListAsync();

            // Calcular ranking
            await CalcularRankingAsync();
        }

        private async Task CalcularRankingAsync()
        {
            // Total de pacientes
            TotalPacientes = await _context.Pacientes.CountAsync();

            // Posição no ranking (pacientes com mais pontos)
            PosicaoRanking = await _context.Pacientes
                .Where(p => p.PsicoPontos > TotalPontos)
                .CountAsync() + 1;

            // Top 10 pacientes (dados anonimizados)
            TopPacientes = await _context.Pacientes
                .Where(p => p.PsicoPontos > 0)
                .OrderByDescending(p => p.PsicoPontos)
                .Take(10)
                .Select(p => new RankingPaciente
                {
                    Nome = p.Nome.Substring(0, 1) + "***", // Anonimizar nome
                    PsicoPontos = p.PsicoPontos,
                    ConsultasRealizadas = p.ConsultasRealizadas
                })
                .ToListAsync();
        }

        public string GetTipoMovimentacaoBadgeClass(TipoMovimentacaoPontos tipo)
        {
            return tipo switch
            {
                TipoMovimentacaoPontos.Ganho => "bg-success",
                TipoMovimentacaoPontos.Uso => "bg-primary",
                TipoMovimentacaoPontos.Bonus => "bg-info",
                TipoMovimentacaoPontos.Expiracao => "bg-warning",
                _ => "bg-secondary"
            };
        }

        public string GetTipoMovimentacaoIcon(TipoMovimentacaoPontos tipo)
        {
            return tipo switch
            {
                TipoMovimentacaoPontos.Ganho => "bi-plus-circle-fill",
                TipoMovimentacaoPontos.Uso => "bi-dash-circle-fill",
                TipoMovimentacaoPontos.Bonus => "bi-gift-fill",
                TipoMovimentacaoPontos.Expiracao => "bi-clock-fill",
                _ => "bi-circle-fill"
            };
        }

        public string GetProgressColorClass()
        {
            var progresso = (10 - PontosParaProximaGratuita) * 10;
            
            return progresso switch
            {
                >= 80 => "bg-success",
                >= 60 => "bg-info",
                >= 40 => "bg-warning",
                _ => "bg-primary"
            };
        }

        public string GetRankingBadgeClass(int posicao)
        {
            return posicao switch
            {
                1 => "bg-warning text-dark", // Ouro
                2 => "bg-secondary", // Prata
                3 => "bg-warning text-dark", // Bronze (usando warning como bronze)
                _ => "bg-primary"
            };
        }
    }
}