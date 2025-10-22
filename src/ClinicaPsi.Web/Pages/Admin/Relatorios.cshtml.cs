using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ClinicaPsi.Application.Services;
using ClinicaPsi.Shared.Models;

namespace ClinicaPsi.Web.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class RelatoriosModel : PageModel
    {
        private readonly ConsultaService _consultaService;
        private readonly PacienteService _pacienteService;
        private readonly PsicologoService _psicologoService;
        private readonly PdfService _pdfService;
        private readonly ILogger<RelatoriosModel> _logger;

        public RelatoriosModel(
            ConsultaService consultaService,
            PacienteService pacienteService,
            PsicologoService psicologoService,
            PdfService pdfService,
            ILogger<RelatoriosModel> logger)
        {
            _consultaService = consultaService;
            _pacienteService = pacienteService;
            _psicologoService = psicologoService;
            _pdfService = pdfService;
            _logger = logger;
        }

        public DateTime? DataInicio { get; set; }
        public DateTime? DataFim { get; set; }
        public string TipoRelatorio { get; set; } = "geral";

        // Dados dos Cards de Resumo
        public decimal ReceitaTotal { get; set; }
        public int TotalConsultas { get; set; }
        public int NovosClientes { get; set; }
        public decimal TaxaRetorno { get; set; }

        // Dados para Gráficos
        public List<ConsultaPorPeriodo> ConsultasPorPeriodo { get; set; } = new();
        public int ConsultasRealizadas { get; set; }
        public int ConsultasCanceladas { get; set; }
        public int ConsultasNoShow { get; set; }
        public int ConsultasAgendadas { get; set; }

        // Relatório Financeiro
        public List<ReceitaPorPsicologoDto> ReceitaPorPsicologo { get; set; } = new();

        // Performance de Psicólogos
        public List<PerformancePsicologoDto> PerformancePsicologos { get; set; } = new();

        // Análise de Pacientes
        public int TotalPontosDistribuidos { get; set; }
        public int ConsultasGratuitasRealizadas { get; set; }
        public decimal MediaPontosPorPaciente { get; set; }

            public async Task<IActionResult> OnGetExportarPdfAsync(DateTime? dataInicio, DateTime? dataFim, string? tipoRelatorio)
            {
                DataFim = dataFim ?? DateTime.Today;
                DataInicio = dataInicio ?? DataFim.Value.AddMonths(-1);
                TipoRelatorio = tipoRelatorio ?? "geral";

                await CarregarDadosResumo();
                await CarregarDadosGraficos();
                switch (TipoRelatorio)
                {
                    case "financeiro":
                        await CarregarRelatorioFinanceiro();
                        break;
                    case "psicologos":
                        await CarregarPerformancePsicologos();
                        break;
                    case "pacientes":
                        await CarregarAnalysePacientes();
                        break;
                }

                var pdfBytes = await _pdfService.GerarRelatorioAdminPdfAsync(this, DataInicio.Value, DataFim.Value, TipoRelatorio);
                var fileName = $"Relatorio_{TipoRelatorio}_{DataInicio.Value:yyyyMMdd}_{DataFim.Value:yyyyMMdd}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }

        public async Task<IActionResult> OnGetAsync(DateTime? dataInicio, DateTime? dataFim, string? tipoRelatorio)
        {
            // Definir período padrão (último mês)
            DataFim = dataFim ?? DateTime.Today;
            DataInicio = dataInicio ?? DataFim.Value.AddMonths(-1);
            TipoRelatorio = tipoRelatorio ?? "geral";

            try
            {
                await CarregarDadosResumo();
                await CarregarDadosGraficos();

                switch (TipoRelatorio)
                {
                    case "financeiro":
                        await CarregarRelatorioFinanceiro();
                        break;
                    case "psicologos":
                        await CarregarPerformancePsicologos();
                        break;
                    case "pacientes":
                        await CarregarAnalysePacientes();
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar relatórios");
                TempData["ErrorMessage"] = "Erro ao carregar os dados dos relatórios.";
            }

            return Page();
        }

        private async Task CarregarDadosResumo()
        {
            var consultas = await _consultaService.GetConsultasByPeriodAsync(DataInicio!.Value, DataFim!.Value);
            
            TotalConsultas = consultas.Count();
            ReceitaTotal = consultas.Where(c => c.Status == StatusConsulta.Realizada).Sum(c => c.Valor);
            
            var pacientes = await _pacienteService.GetAllAsync();
            NovosClientes = pacientes.Count(p => p.DataCadastro >= DataInicio!.Value && p.DataCadastro <= DataFim!.Value);
            
            // Calcular taxa de retorno (pacientes com mais de uma consulta)
            var pacientesComConsultas = consultas.GroupBy(c => c.PacienteId).Count();
            var pacientesComMultiplasConsultas = consultas.GroupBy(c => c.PacienteId).Count(g => g.Count() > 1);
            TaxaRetorno = pacientesComConsultas > 0 ? (decimal)pacientesComMultiplasConsultas / pacientesComConsultas * 100 : 0;
        }

        private async Task CarregarDadosGraficos()
        {
            var consultas = await _consultaService.GetConsultasByPeriodAsync(DataInicio!.Value, DataFim!.Value);
            
            // Consultas por período (agrupado por dia)
            ConsultasPorPeriodo = consultas
                .GroupBy(c => c.DataHorario.Date)
                .Select(g => new ConsultaPorPeriodo
                {
                    Data = g.Key,
                    Quantidade = g.Count()
                })
                .OrderBy(x => x.Data)
                .ToList();

            // Status das consultas
            ConsultasRealizadas = consultas.Count(c => c.Status == StatusConsulta.Realizada);
            ConsultasCanceladas = consultas.Count(c => c.Status == StatusConsulta.Cancelada);
            ConsultasNoShow = consultas.Count(c => c.Status == StatusConsulta.NoShow);
            ConsultasAgendadas = consultas.Count(c => c.Status == StatusConsulta.Agendada || c.Status == StatusConsulta.Confirmada);
        }

        private async Task CarregarRelatorioFinanceiro()
        {
            var consultas = await _consultaService.GetConsultasByPeriodAsync(DataInicio!.Value, DataFim!.Value);
            var psicologos = await _psicologoService.GetAllAsync();

            ReceitaPorPsicologo = psicologos.Select(p => new ReceitaPorPsicologoDto
            {
                Nome = p.Nome,
                Consultas = consultas.Count(c => c.PsicologoId == p.Id && c.Status == StatusConsulta.Realizada),
                Receita = consultas.Where(c => c.PsicologoId == p.Id && c.Status == StatusConsulta.Realizada).Sum(c => c.Valor)
            }).OrderByDescending(x => x.Receita).ToList();
        }

        private async Task CarregarPerformancePsicologos()
        {
            var consultas = await _consultaService.GetConsultasByPeriodAsync(DataInicio!.Value, DataFim!.Value);
            var psicologos = await _psicologoService.GetAllAsync();

            PerformancePsicologos = psicologos.Select(p =>
            {
                var consultasPsicologo = consultas.Where(c => c.PsicologoId == p.Id).ToList();
                var totalConsultas = consultasPsicologo.Count;
                var consultasCanceladas = consultasPsicologo.Count(c => c.Status == StatusConsulta.Cancelada || c.Status == StatusConsulta.NoShow);

                return new PerformancePsicologoDto
                {
                    Nome = p.Nome,
                    Ativo = p.Ativo,
                    ConsultasRealizadas = consultasPsicologo.Count(c => c.Status == StatusConsulta.Realizada),
                    TaxaCancelamento = totalConsultas > 0 ? (decimal)consultasCanceladas / totalConsultas * 100 : 0,
                    AvaliacaoMedia = 4.5m, // Placeholder - implementar sistema de avaliação
                    ReceitaGerada = consultasPsicologo.Where(c => c.Status == StatusConsulta.Realizada).Sum(c => c.Valor)
                };
            }).OrderByDescending(x => x.ConsultasRealizadas).ToList();
        }

        private async Task CarregarAnalysePacientes()
        {
            var pacientes = await _pacienteService.GetAllAsync();
            
            TotalPontosDistribuidos = pacientes.Sum(p => p.PsicoPontos);
            ConsultasGratuitasRealizadas = pacientes.Sum(p => p.ConsultasGratuitas);
            MediaPontosPorPaciente = pacientes.Any() ? (decimal)pacientes.Average(p => p.PsicoPontos) : 0;
        }

        // DTOs para os relatórios
        public class ConsultaPorPeriodo
        {
            public DateTime Data { get; set; }
            public int Quantidade { get; set; }
        }

        public class ReceitaPorPsicologoDto
        {
            public string Nome { get; set; } = string.Empty;
            public int Consultas { get; set; }
            public decimal Receita { get; set; }
        }

        public class PerformancePsicologoDto
        {
            public string Nome { get; set; } = string.Empty;
            public bool Ativo { get; set; }
            public int ConsultasRealizadas { get; set; }
            public decimal TaxaCancelamento { get; set; }
            public decimal AvaliacaoMedia { get; set; }
            public decimal ReceitaGerada { get; set; }
        }
    }
}