using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using ClinicaPsi.Web.Extensions;
using ClinicaPsi.Application.Services;
using System.Security.Claims;

namespace ClinicaPsi.Web.Pages.Cliente
{
    [Authorize]
    public class HistoricoModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly PdfService _pdfService;

        public HistoricoModel(AppDbContext context, PdfService pdfService)
        {
            _context = context;
            _pdfService = pdfService;
        }

        public List<Consulta> ConsultasHistorico { get; set; } = new();
        public List<HistoricoPontos> HistoricoPontos { get; set; } = new();
        public Paciente? PacienteAtual { get; set; }
        public DateTime DataInicio { get; set; } = DateTime.Now.AddMonths(-6);
        public DateTime DataFim { get; set; } = DateTime.Now;
        public string FiltroTipo { get; set; } = "todos";
        public int PaginaAtual { get; set; } = 1;
        public int TotalPaginas { get; set; }
        public int TotalRegistros { get; set; }
        public const int ItensPorPagina = 10;

        // Estatísticas
        public int TotalConsultasRealizadas { get; set; }
        public decimal TotalGasto { get; set; }
        public int ConsultasGratuitas { get; set; }
        public int TotalPontosGanhos { get; set; }
        public int TotalPontosUsados { get; set; }
        public Dictionary<string, int> ConsultasPorPsicologo { get; set; } = new();
        public Dictionary<string, int> ConsultasPorMes { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(DateTime? dataInicio, DateTime? dataFim,
            string filtroTipo = "todos", int pagina = 1)
        {
            try
            {
                if (dataInicio.HasValue) DataInicio = dataInicio.Value;
                if (dataFim.HasValue) DataFim = dataFim.Value;
                FiltroTipo = filtroTipo;
                PaginaAtual = pagina;

                await CarregarDadosAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao carregar Historico: {ex.Message}");
                // Define valores padrão
                ConsultasHistorico = new List<Consulta>();
                HistoricoPontos = new List<HistoricoPontos>();
            }
            return Page();
        }

        public async Task<IActionResult> OnGetExportarPdfAsync(DateTime? dataInicio, DateTime? dataFim)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

                if (user?.PacienteId == null)
                {
                    TempData["Erro"] = "Paciente não encontrado.";
                    return RedirectToPage();
                }

                var pdfBytes = await _pdfService.GerarHistoricoConsultasPacienteAsync(
                    user.PacienteId.Value,
                    dataInicio,
                    dataFim);

                var paciente = await _context.Pacientes.FindAsync(user.PacienteId.Value);
                var nomeArquivo = $"Historico_{paciente?.Nome?.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.pdf";

                return File(pdfBytes, "application/pdf", nomeArquivo);
            }
            catch (Exception ex)
            {
                TempData["Erro"] = $"Erro ao gerar PDF: {ex.Message}";
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

            // Carregar consultas do histórico
            var consultasQuery = _context.Consultas
                .Include(c => c.Psicologo)
                .Where(c => c.PacienteId == user.PacienteId.Value &&
                           c.DataHorario >= DataInicio &&
                           c.DataHorario <= DataFim.AddDays(1));

            // Aplicar filtro por tipo
            if (FiltroTipo != "todos")
            {
                if (Enum.TryParse<TipoConsulta>(FiltroTipo, true, out var tipoConsulta))
                {
                    consultasQuery = consultasQuery.Where(c => c.Tipo == tipoConsulta);
                }
            }

            // Contar total de registros
            TotalRegistros = await consultasQuery.CountAsync();
            TotalPaginas = (int)Math.Ceiling((double)TotalRegistros / ItensPorPagina);

            // Aplicar paginação
            ConsultasHistorico = await consultasQuery
                .OrderByDescending(c => c.DataHorario)
                .Skip((PaginaAtual - 1) * ItensPorPagina)
                .Take(ItensPorPagina)
                .ToListAsync();

            // Carregar histórico de pontos
            HistoricoPontos = await _context.HistoricoPontos
                .Where(h => h.PacienteId == user.PacienteId.Value &&
                           h.DataMovimentacao >= DataInicio &&
                           h.DataMovimentacao <= DataFim.AddDays(1))
                .OrderByDescending(h => h.DataMovimentacao)
                .Take(20)
                .ToListAsync();

            // Calcular estatísticas
            await CalcularEstatisticasAsync(user.PacienteId.Value);
        }

        private async Task CalcularEstatisticasAsync(int pacienteId)
        {
            var consultasRealizadas = await _context.Consultas
                .Include(c => c.Psicologo)
                .Where(c => c.PacienteId == pacienteId &&
                           c.Status == StatusConsulta.Realizada &&
                           c.DataHorario >= DataInicio &&
                           c.DataHorario <= DataFim.AddDays(1))
                .ToListAsync();

            TotalConsultasRealizadas = consultasRealizadas.Count;
            TotalGasto = consultasRealizadas.Sum(c => c.Valor);
            ConsultasGratuitas = consultasRealizadas.Count(c => c.Tipo == TipoConsulta.Gratuita);

            // Pontos ganhos e usados no período
            var movimentacoesPontos = await _context.HistoricoPontos
                .Where(h => h.PacienteId == pacienteId &&
                           h.DataMovimentacao >= DataInicio &&
                           h.DataMovimentacao <= DataFim.AddDays(1))
                .ToListAsync();

            TotalPontosGanhos = movimentacoesPontos
                .Where(h => h.TipoMovimentacao == TipoMovimentacaoPontos.Ganho)
                .Sum(h => h.Pontos);

            TotalPontosUsados = movimentacoesPontos
                .Where(h => h.TipoMovimentacao == TipoMovimentacaoPontos.Uso)
                .Sum(h => h.Pontos);

            // Consultas por psicólogo
            ConsultasPorPsicologo = consultasRealizadas
                .GroupBy(c => c.Psicologo.Nome)
                .ToDictionary(g => g.Key, g => g.Count());

            // Consultas por mês
            ConsultasPorMes = consultasRealizadas
                .GroupBy(c => c.DataHorario.ToString("MM/yyyy"))
                .ToDictionary(g => g.Key, g => g.Count());
        }

        public string GetStatusBadgeClass(StatusConsulta status)
        {
            return status switch
            {
                StatusConsulta.Realizada => "bg-success",
                StatusConsulta.Cancelada => "bg-danger",
                StatusConsulta.NoShow => "bg-warning",
                _ => "bg-secondary"
            };
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
                TipoMovimentacaoPontos.Ganho => "bi-plus-circle",
                TipoMovimentacaoPontos.Uso => "bi-dash-circle",
                TipoMovimentacaoPontos.Bonus => "bi-gift",
                TipoMovimentacaoPontos.Expiracao => "bi-clock",
                _ => "bi-circle"
            };
        }
    }
}
