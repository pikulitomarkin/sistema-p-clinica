
using System;
using System.Threading.Tasks;
using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ClinicaPsi.Application.Services
{
    public class PdfService
    {
        private readonly AppDbContext _context;

        public PdfService(AppDbContext context)
        {
            _context = context;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        // Handler PDF Admin com dados reais
        public async Task<byte[]> GerarRelatorioAdminPdfAsync(object model, DateTime inicio, DateTime fim, string tipo)
        {
            // Buscar dados reais do banco
            var consultas = await _context.Consultas
                .Include(c => c.Paciente)
                .Include(c => c.Psicologo)
                .Where(c => c.DataHorario >= inicio && c.DataHorario <= fim)
                .ToListAsync();

            var totalConsultas = consultas.Count;
            var consultasRealizadas = consultas.Count(c => c.Status == StatusConsulta.Realizada);
            var consultasCanceladas = consultas.Count(c => c.Status == StatusConsulta.Cancelada);
            var receitaTotal = consultas.Where(c => c.Status == StatusConsulta.Realizada).Sum(c => c.Valor);
            var psicologoStats = consultas.GroupBy(c => c.Psicologo?.Nome ?? "Não atribuído")
                .Select(g => new { Psicologo = g.Key, Consultas = g.Count(), Receita = g.Sum(c => c.Valor) })
                .ToList();

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header()
                        .Column(col =>
                        {
                            col.Item().Text("CLÍNICA PSII ANA SANTOS").FontSize(18).Bold().FontColor(Colors.Green.Medium);
                            col.Item().Text("RELATÓRIO ADMINISTRATIVO").FontSize(14).Bold();
                        });

                    page.Content()
                        .Column(col =>
                        {
                            col.Spacing(12);
                            
                            // Período
                            col.Item().Row(row =>
                            {
                                row.RelativeItem().Text($"Período: {inicio:dd/MM/yyyy} a {fim:dd/MM/yyyy}").Bold();
                                row.RelativeItem().Text($"Tipo: {tipo}").Bold();
                            });

                            col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                            // Resumo
                            col.Item().Text("RESUMO GERAL").FontSize(12).Bold().FontColor(Colors.Green.Medium);
                            col.Item().Row(row =>
                            {
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text("Total de Consultas").FontSize(9).Bold();
                                    c.Item().Text($"{totalConsultas}").FontSize(14).Bold().FontColor(Colors.Green.Medium);
                                });
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text("Realizadas").FontSize(9).Bold();
                                    c.Item().Text($"{consultasRealizadas}").FontSize(14).Bold().FontColor(Colors.Green.Medium);
                                });
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text("Canceladas").FontSize(9).Bold();
                                    c.Item().Text($"{consultasCanceladas}").FontSize(14).Bold().FontColor(Colors.Red.Medium);
                                });
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text("Receita Total").FontSize(9).Bold();
                                    c.Item().Text($"R$ {receitaTotal:N2}").FontSize(14).Bold().FontColor(Colors.Green.Medium);
                                });
                            });

                            col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                            // Tabela de Psicólogos
                            if (psicologoStats.Any())
                            {
                                col.Item().Text("CONSULTAS POR PSICÓLOGO").FontSize(12).Bold().FontColor(Colors.Green.Medium);
                                
                                col.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(1);
                                    });

                                    // Cabeçalho
                                    table.Header(header =>
                                    {
                                        header.Cell().Background(Colors.Green.Medium).Padding(8).Text("Psicólogo").FontColor(Colors.White).Bold();
                                        header.Cell().Background(Colors.Green.Medium).Padding(8).Text("Consultas").FontColor(Colors.White).Bold();
                                        header.Cell().Background(Colors.Green.Medium).Padding(8).Text("Receita").FontColor(Colors.White).Bold();
                                    });

                                    // Linhas
                                    foreach (var item in psicologoStats)
                                    {
                                        var bgColor = psicologoStats.IndexOf(item) % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;
                                        table.Cell().Background(bgColor).Padding(5).Text(item.Psicologo);
                                        table.Cell().Background(bgColor).Padding(5).Text($"{item.Consultas}").AlignCenter();
                                        table.Cell().Background(bgColor).Padding(5).Text($"R$ {item.Receita:N2}").AlignRight();
                                    }
                                });
                            }

                            col.Item().PaddingTop(15).Text($"Data de Geração: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(9).FontColor(Colors.Grey.Medium);
                        });

                    page.Footer()
                        .Column(col =>
                        {
                            col.Item()
                                .AlignCenter()
                                .Text("Relatório gerado automaticamente")
                                .FontSize(9)
                                .FontColor(Colors.Grey.Medium);
                        });
                });
            });

            return document.GeneratePdf();
        }

        /// <summary>
        /// Gera PDF do histórico de consultas do paciente
        /// </summary>
        public async Task<byte[]> GerarHistoricoConsultasPacienteAsync(int pacienteId, DateTime? dataInicio = null, DateTime? dataFim = null)
        {
            var paciente = await _context.Pacientes.FindAsync(pacienteId);
            if (paciente == null)
                throw new Exception("Paciente não encontrado");

            var query = _context.Consultas
                .Include(c => c.Psicologo)
                .Where(c => c.PacienteId == pacienteId);

            if (dataInicio.HasValue)
                query = query.Where(c => c.DataHorario >= dataInicio.Value);

            if (dataFim.HasValue)
                query = query.Where(c => c.DataHorario <= dataFim.Value);

            var consultas = await query.OrderByDescending(c => c.DataHorario).ToListAsync();

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header()
                        .Element(ComposeHeader);

                    page.Content()
                        .Element(c => ComposeContent(c, paciente, consultas, dataInicio, dataFim));

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Página ");
                            x.CurrentPageNumber();
                            x.Span(" de ");
                            x.TotalPages();
                        });
                });
            });

            return document.GeneratePdf();
        }

        private void ComposeHeader(IContainer container)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text("PsiiAnaSantos").FontSize(20).Bold().FontColor(Colors.Green.Medium);
                    column.Item().Text("Clínica de Psicologia").FontSize(12).FontColor(Colors.Grey.Medium);
                    column.Item().Text("Psi. Ana Santos - CRP 08/45168").FontSize(10);
                });

                row.ConstantItem(140).Column(column =>
                {
                    // ... pode adicionar logo ou outros dados ...
                });
            });
        }

        private void ComposeContent(IContainer container, object paciente, object consultas, DateTime? dataInicio, DateTime? dataFim)
        {
            // Implementação do conteúdo do PDF do paciente
        }

        private void ComposeRelatorioContent(
            IContainer container,
            DateTime dataInicio,
            DateTime dataFim,
            int totalConsultas,
            decimal receitaTotal,
            int novosClientes,
            int consultasRealizadas,
            int consultasCanceladas,
            int consultasNoShow,
            int consultasAgendadas,
            List<ReceitaPorPsicologoDto> receitaPorPsicologo)
        {
            container.PaddingVertical(20).Column(column =>
            {
                column.Spacing(10);
                // ... conteúdo do relatório administrativo ...
            });
        }
    }

    public class ReceitaPorPsicologoDto
    {
        public string Nome { get; set; } = string.Empty;
        public int Consultas { get; set; }
        public decimal Receita { get; set; }
    }
}
