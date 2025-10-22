using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ClinicaPsi.Application.Services;

public class PdfService
{
    private readonly AppDbContext _context;

    public PdfService(AppDbContext context)
    {
        _context = context;
        
        // Configurar licença gratuita do QuestPDF para projetos não-comerciais
        QuestPDF.Settings.License = LicenseType.Community;
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
                column.Item().Text(text => text.Span("Contato:").FontSize(10).Bold());
                column.Item().Text(text => text.Span("(42) 99936-9724").FontSize(9));
                column.Item().Text(text => text.Span("psiana@psiianasantos.com.br").FontSize(9));
                column.Item().Text(text =>
                {
                    text.Span("Data: ").Bold().FontSize(9);
                    text.Span(DateTime.Now.ToString("dd/MM/yyyy")).FontSize(9);
                });
            });
        });
    }

    private void ComposeContent(IContainer container, Paciente paciente, List<Consulta> consultas, DateTime? dataInicio, DateTime? dataFim)
    {
        container.PaddingVertical(20).Column(column =>
        {
            column.Spacing(10);

            // Título do relatório
            column.Item().Text("HISTÓRICO DE CONSULTAS").FontSize(16).Bold().FontColor(Colors.Green.Medium);
            
            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

            // Dados do paciente
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(text =>
                    {
                        text.Span("Paciente: ").Bold();
                        text.Span(paciente.Nome);
                    });
                    col.Item().Text(text =>
                    {
                        text.Span("CPF: ").Bold();
                        text.Span(paciente.CPF);
                    });
                    col.Item().Text(text =>
                    {
                        text.Span("Email: ").Bold();
                        text.Span(paciente.Email ?? "Não informado");
                    });
                });

                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(text =>
                    {
                        text.Span("Telefone: ").Bold();
                        text.Span(paciente.Telefone ?? "Não informado");
                    });
                    col.Item().Text(text =>
                    {
                        text.Span("PsicoPontos: ").Bold().FontColor(Colors.Green.Medium);
                        text.Span($"{paciente.PsicoPontos} pontos").FontColor(Colors.Green.Medium);
                    });
                    col.Item().Text(text =>
                    {
                        text.Span("Consultas Gratuitas: ").Bold().FontColor(Colors.Green.Medium);
                        text.Span($"{paciente.ConsultasGratuitas}").FontColor(Colors.Green.Medium);
                    });
                });
            });

            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

            // Período do relatório
            if (dataInicio.HasValue || dataFim.HasValue)
            {
                column.Item().Text(text =>
                {
                    text.Span("Período: ").Bold();
                    text.Span($"{dataInicio?.ToString("dd/MM/yyyy") ?? "Início"} até {dataFim?.ToString("dd/MM/yyyy") ?? "Presente"}");
                });
            }

            // Estatísticas
            column.Item().Row(row =>
            {
                var totalConsultas = consultas.Count;
                var realizadas = consultas.Count(c => c.Status == StatusConsulta.Realizada);
                var canceladas = consultas.Count(c => c.Status == StatusConsulta.Cancelada);
                var valorTotal = consultas.Where(c => c.Status == StatusConsulta.Realizada).Sum(c => c.Valor);

                row.RelativeItem().Background(Colors.Green.Lighten4).Padding(10).Column(col =>
                {
                    col.Item().Text("Total de Consultas").FontSize(10).Bold();
                    col.Item().Text($"{totalConsultas}").FontSize(14).Bold();
                });

                row.RelativeItem().Background(Colors.Blue.Lighten4).Padding(10).Column(col =>
                {
                    col.Item().Text("Realizadas").FontSize(10).Bold();
                    col.Item().Text($"{realizadas}").FontSize(14).Bold();
                });

                row.RelativeItem().Background(Colors.Red.Lighten4).Padding(10).Column(col =>
                {
                    col.Item().Text("Canceladas").FontSize(10).Bold();
                    col.Item().Text($"{canceladas}").FontSize(14).Bold();
                });

                row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(10).Column(col =>
                {
                    col.Item().Text("Valor Total").FontSize(10).Bold();
                    col.Item().Text($"R$ {valorTotal:F2}").FontSize(14).Bold();
                });
            });

            column.Item().PaddingTop(20).Text("CONSULTAS REALIZADAS").FontSize(14).Bold();

            // Tabela de consultas
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(80); // Data
                    columns.RelativeColumn(); // Psicólogo
                    columns.ConstantColumn(70); // Tipo
                    columns.ConstantColumn(70); // Status
                    columns.ConstantColumn(70); // Valor
                });

                // Cabeçalho
                table.Header(header =>
                {
                    header.Cell().Background(Colors.Green.Medium).Padding(5).Text("Data").FontColor(Colors.White).Bold();
                    header.Cell().Background(Colors.Green.Medium).Padding(5).Text("Psicólogo").FontColor(Colors.White).Bold();
                    header.Cell().Background(Colors.Green.Medium).Padding(5).Text("Tipo").FontColor(Colors.White).Bold();
                    header.Cell().Background(Colors.Green.Medium).Padding(5).Text("Status").FontColor(Colors.White).Bold();
                    header.Cell().Background(Colors.Green.Medium).Padding(5).Text("Valor").FontColor(Colors.White).Bold();
                });

                // Linhas
                foreach (var consulta in consultas)
                {
                    var backgroundColor = consultas.IndexOf(consulta) % 2 == 0 ? Colors.Grey.Lighten5 : Colors.White;

                    table.Cell().Background(backgroundColor).Padding(5).Text(consulta.DataHorario.ToString("dd/MM/yyyy"));
                    table.Cell().Background(backgroundColor).Padding(5).Text(consulta.Psicologo?.Nome ?? "N/A");
                    table.Cell().Background(backgroundColor).Padding(5).Text(consulta.Tipo.ToString());
                    table.Cell().Background(backgroundColor).Padding(5).Text(consulta.Status.ToString());
                    table.Cell().Background(backgroundColor).Padding(5).Text($"R$ {consulta.Valor:F2}");
                }
            });

            // Observação sobre PsicoPontos
            column.Item().PaddingTop(20).Background(Colors.Green.Lighten5).Padding(10).Column(col =>
            {
                col.Item().Text("💡 SISTEMA PSICOPONTOS").FontSize(12).Bold().FontColor(Colors.Green.Medium);
                col.Item().Text("A cada consulta realizada você ganha 1 ponto.").FontSize(10);
                col.Item().Text("Acumule 10 pontos e ganhe 1 consulta gratuita!").FontSize(10);
                col.Item().PaddingTop(5).Text(text =>
                {
                    text.Span("Pontos acumulados: ").Bold();
                    text.Span($"{paciente.PsicoPontos} pontos");
                });
                col.Item().Text(text =>
                {
                    text.Span("Consultas gratuitas disponíveis: ").Bold();
                    text.Span($"{paciente.ConsultasGratuitas}");
                });
            });
        });
    }

    /// <summary>
    /// Gera PDF do relatório administrativo
    /// </summary>
    public byte[] GerarRelatorioAdministrativo(
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
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Element(ComposeHeader);
                page.Content().Element(c => ComposeRelatorioContent(c, dataInicio, dataFim, totalConsultas, 
                    receitaTotal, novosClientes, consultasRealizadas, consultasCanceladas, 
                    consultasNoShow, consultasAgendadas, receitaPorPsicologo));
                page.Footer().AlignCenter().Text(x =>
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

            // Título
            column.Item().Text("RELATÓRIO ADMINISTRATIVO").FontSize(16).Bold().FontColor(Colors.Green.Medium);
            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

            // Período
            column.Item().Text(text =>
            {
                text.Span("Período: ").Bold();
                text.Span($"{dataInicio:dd/MM/yyyy} até {dataFim:dd/MM/yyyy}");
            });

            column.Item().PaddingTop(10);

            // Cards de resumo
            column.Item().Row(row =>
            {
                row.RelativeItem().Background(Colors.Green.Lighten4).Padding(10).Column(col =>
                {
                    col.Item().Text("Receita Total").FontSize(10).Bold();
                    col.Item().Text($"R$ {receitaTotal:N2}").FontSize(16).Bold().FontColor(Colors.Green.Darken2);
                });

                row.Spacing(5);

                row.RelativeItem().Background(Colors.Blue.Lighten4).Padding(10).Column(col =>
                {
                    col.Item().Text("Total Consultas").FontSize(10).Bold();
                    col.Item().Text($"{totalConsultas}").FontSize(16).Bold().FontColor(Colors.Blue.Darken2);
                });

                row.Spacing(5);

                row.RelativeItem().Background(Colors.Orange.Lighten4).Padding(10).Column(col =>
                {
                    col.Item().Text("Novos Clientes").FontSize(10).Bold();
                    col.Item().Text($"{novosClientes}").FontSize(16).Bold().FontColor(Colors.Orange.Darken2);
                });
            });

            // Status das consultas
            column.Item().PaddingTop(15).Text("STATUS DAS CONSULTAS").FontSize(14).Bold();
            
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(text =>
                    {
                        text.Span("✓ Realizadas: ").Bold().FontColor(Colors.Green.Medium);
                        text.Span($"{consultasRealizadas}");
                    });
                    col.Item().Text(text =>
                    {
                        text.Span("✗ Canceladas: ").Bold().FontColor(Colors.Red.Medium);
                        text.Span($"{consultasCanceladas}");
                    });
                });

                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(text =>
                    {
                        text.Span("⊘ No-Show: ").Bold().FontColor(Colors.Orange.Medium);
                        text.Span($"{consultasNoShow}");
                    });
                    col.Item().Text(text =>
                    {
                        text.Span("◷ Agendadas: ").Bold().FontColor(Colors.Blue.Medium);
                        text.Span($"{consultasAgendadas}");
                    });
                });
            });

            // Receita por psicólogo
            if (receitaPorPsicologo.Any())
            {
                column.Item().PaddingTop(20).Text("RECEITA POR PSICÓLOGO").FontSize(14).Bold();

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                    });

                    // Cabeçalho
                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Green.Medium).Padding(5).Text("Psicólogo").FontColor(Colors.White).Bold();
                        header.Cell().Background(Colors.Green.Medium).Padding(5).Text("Consultas").FontColor(Colors.White).Bold();
                        header.Cell().Background(Colors.Green.Medium).Padding(5).Text("Receita").FontColor(Colors.White).Bold();
                    });

                    // Linhas
                    foreach (var item in receitaPorPsicologo)
                    {
                        var backgroundColor = receitaPorPsicologo.IndexOf(item) % 2 == 0 ? Colors.Grey.Lighten5 : Colors.White;

                        table.Cell().Background(backgroundColor).Padding(5).Text(item.Nome);
                        table.Cell().Background(backgroundColor).Padding(5).Text($"{item.Consultas}");
                        table.Cell().Background(backgroundColor).Padding(5).Text($"R$ {item.Receita:N2}");
                    }

                    // Total
                    table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("TOTAL").Bold();
                    table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text($"{receitaPorPsicologo.Sum(x => x.Consultas)}").Bold();
                    table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text($"R$ {receitaPorPsicologo.Sum(x => x.Receita):N2}").Bold();
                });
            }

            // Rodapé
            column.Item().PaddingTop(20).Background(Colors.Blue.Lighten5).Padding(10).Column(col =>
            {
                col.Item().Text("ℹ️ RELATÓRIO GERADO AUTOMATICAMENTE").FontSize(10).Bold().FontColor(Colors.Blue.Medium);
                col.Item().Text($"Data de geração: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(9);
                col.Item().Text("PsiiAnaSantos - Sistema de Gestão de Clínica de Psicologia").FontSize(9);
            });
        });
    }
}

public class ReceitaPorPsicologoDto
{
    public string Nome { get; set; } = string.Empty;
    public int Consultas { get; set; }
    public decimal Receita { get; set; }
}
