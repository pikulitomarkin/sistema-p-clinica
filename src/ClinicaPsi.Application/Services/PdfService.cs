
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

        /// <summary>
        /// Gera PDF do prontuário eletrônico
        /// </summary>
        public async Task<byte[]> GerarProntuarioPdfAsync(int prontuarioId)
        {
            var prontuario = await _context.ProntuariosEletronicos
                .Include(p => p.Paciente)
                .Include(p => p.Psicologo)
                .FirstOrDefaultAsync(p => p.Id == prontuarioId);

            if (prontuario == null)
                throw new Exception("Prontuário não encontrado");

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    // Header
                    page.Header().Column(col =>
                    {
                        col.Item().AlignCenter().Text("PRONTUÁRIO ELETRÔNICO").FontSize(18).Bold().FontColor(Colors.Green.Medium);
                        col.Item().AlignCenter().Text("Clínica PsiiAnaSantos").FontSize(12).FontColor(Colors.Grey.Medium);
                        col.Item().PaddingTop(5).LineHorizontal(2).LineColor(Colors.Green.Medium);
                    });

                    // Content
                    page.Content().PaddingTop(10).Column(col =>
                    {
                        col.Spacing(8);

                        // Dados do Paciente
                        col.Item().Text("DADOS DO PACIENTE").FontSize(14).Bold().FontColor(Colors.Green.Medium);
                        col.Item().Background(Colors.Grey.Lighten4).Padding(10).Column(c =>
                        {
                            c.Item().Row(row =>
                            {
                                row.RelativeItem().Text($"Nome: {prontuario.Paciente?.Nome ?? "N/A"}").Bold();
                                row.ConstantItem(150).Text($"Data: {prontuario.DataSessao:dd/MM/yyyy}").AlignRight();
                            });
                            c.Item().Text($"CPF: {prontuario.Paciente?.CPF ?? "N/A"}");
                            c.Item().Text($"Telefone: {prontuario.Paciente?.Telefone ?? "N/A"}");
                        });

                        // Dados do Profissional
                        col.Item().PaddingTop(10).Text("PROFISSIONAL RESPONSÁVEL").FontSize(14).Bold().FontColor(Colors.Green.Medium);
                        col.Item().Background(Colors.Grey.Lighten4).Padding(10).Column(c =>
                        {
                            c.Item().Text($"Psicólogo(a): {prontuario.Psicologo?.Nome ?? "N/A"}").Bold();
                            c.Item().Text($"CRP: {prontuario.Psicologo?.CRP ?? "N/A"}");
                        });

                        // Tipo de Atendimento
                        col.Item().PaddingTop(10).Text($"Tipo de Atendimento: {prontuario.TipoAtendimento}").FontSize(12).SemiBold();

                        // Queixa Principal
                        if (!string.IsNullOrEmpty(prontuario.QueixaPrincipal))
                        {
                            col.Item().PaddingTop(10).Text("QUEIXA PRINCIPAL").FontSize(14).Bold().FontColor(Colors.Green.Medium);
                            col.Item().Background(Colors.Grey.Lighten5).Padding(10).Text(prontuario.QueixaPrincipal).FontSize(10);
                        }

                        // Intervenções Realizadas
                        if (!string.IsNullOrEmpty(prontuario.Intervencoes))
                        {
                            col.Item().PaddingTop(10).Text("INTERVENÇÕES REALIZADAS").FontSize(14).Bold().FontColor(Colors.Green.Medium);
                            col.Item().Background(Colors.Grey.Lighten5).Padding(10).Text(prontuario.Intervencoes).FontSize(10);
                        }

                        // Plano Terapêutico
                        if (!string.IsNullOrEmpty(prontuario.PlanoTerapeutico))
                        {
                            col.Item().PaddingTop(10).Text("PLANO TERAPÊUTICO").FontSize(14).Bold().FontColor(Colors.Green.Medium);
                            col.Item().Background(Colors.Grey.Lighten5).Padding(10).Text(prontuario.PlanoTerapeutico).FontSize(10);
                        }

                        // Observações
                        if (!string.IsNullOrEmpty(prontuario.Observacoes))
                        {
                            col.Item().PaddingTop(10).Text("OBSERVAÇÕES").FontSize(14).Bold().FontColor(Colors.Green.Medium);
                            col.Item().Background(Colors.Grey.Lighten5).Padding(10).Text(prontuario.Observacoes).FontSize(10);
                        }

                        // Evolução
                        if (!string.IsNullOrEmpty(prontuario.Evolucao))
                        {
                            col.Item().PaddingTop(10).Text("EVOLUÇÃO").FontSize(14).Bold().FontColor(Colors.Green.Medium);
                            col.Item().Background(Colors.Grey.Lighten5).Padding(10).Text(prontuario.Evolucao).FontSize(10);
                        }

                        // Status
                        col.Item().PaddingTop(15).Row(row =>
                        {
                            row.RelativeItem().Text($"Status: {(prontuario.Finalizado ? "FINALIZADO" : "EM ANDAMENTO")}")
                                .Bold()
                                .FontColor(prontuario.Finalizado ? Colors.Green.Medium : Colors.Orange.Medium);
                            
                            row.ConstantItem(200).Text($"Gerado em: {DateTime.Now:dd/MM/yyyy HH:mm}")
                                .FontSize(9)
                                .FontColor(Colors.Grey.Medium)
                                .AlignRight();
                        });
                    });

                    // Footer
                    page.Footer().AlignCenter().Column(col =>
                    {
                        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                        col.Item().PaddingTop(5).Text($"Documento gerado em: {DateTime.Now:dd/MM/yyyy HH:mm}")
                            .FontSize(9)
                            .FontColor(Colors.Grey.Medium);
                        col.Item().Text("Este documento é confidencial e protegido pelo sigilo profissional")
                            .FontSize(8)
                            .FontColor(Colors.Grey.Medium)
                            .Italic();
                    });
                });
            });

            return document.GeneratePdf();
        }

        // Gerar Declaração de Comparecimento
        public async Task<byte[]> GerarDeclaracaoComparecimentoAsync(int consultaId)
    {
        var consulta = await _context.Consultas
            .Include(c => c.Paciente)
            .Include(c => c.Psicologo)
            .FirstOrDefaultAsync(c => c.Id == consultaId);

        if (consulta == null)
            throw new ArgumentException("Consulta não encontrada");

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(12));

                page.Header()
                    .Column(col =>
                    {
                        col.Item().AlignCenter().Text("DECLARAÇÃO DE COMPARECIMENTO").FontSize(18).Bold();
                        col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                    });

                page.Content()
                    .PaddingTop(40)
                    .Column(col =>
                    {
                        col.Spacing(20);

                        // Texto da declaração
                        col.Item().Text(text =>
                        {
                            text.Span("Declaro para os devidos fins que ").FontSize(12);
                            text.Span(consulta.Paciente?.Nome ?? "").Bold().FontSize(12);
                            text.Span(", portador(a) do CPF ").FontSize(12);
                            text.Span(consulta.Paciente?.CPF ?? "").Bold().FontSize(12);
                            text.Span(", compareceu à consulta psicológica no dia ").FontSize(12);
                            text.Span(consulta.DataHorario.ToString("dd/MM/yyyy")).Bold().FontSize(12);
                            text.Span(" às ").FontSize(12);
                            text.Span(consulta.DataHorario.ToString("HH:mm")).Bold().FontSize(12);
                            text.Span(" com duração de ").FontSize(12);
                            text.Span($"{consulta.DuracaoMinutos} minutos").Bold().FontSize(12);
                            text.Span(".").FontSize(12);
                        });

                        col.Item().PaddingTop(60).AlignCenter().Text(text =>
                        {
                            text.Span("São Paulo, ").FontSize(12);
                            text.Span(DateTime.Now.ToString("dd 'de' MMMM 'de' yyyy", new System.Globalization.CultureInfo("pt-BR"))).FontSize(12);
                        });

                        // Assinatura
                        col.Item().PaddingTop(60).AlignCenter().Column(signCol =>
                        {
                            signCol.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken3);
                            signCol.Item().PaddingTop(5).Text(consulta.Psicologo?.Nome ?? "").Bold().FontSize(12);
                            signCol.Item().Text($"CRP: {consulta.Psicologo?.CRP ?? ""}").FontSize(11);
                            signCol.Item().Text($"Email: {consulta.Psicologo?.Email ?? ""}").FontSize(10).FontColor(Colors.Grey.Darken2);
                            signCol.Item().Text($"Telefone: {consulta.Psicologo?.Telefone ?? ""}").FontSize(10).FontColor(Colors.Grey.Darken2);
                        });
                    });

                page.Footer()
                    .AlignCenter()
                    .Text(text =>
                    {
                        text.Span("Documento gerado eletronicamente em ").FontSize(8).FontColor(Colors.Grey.Medium);
                        text.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(8).FontColor(Colors.Grey.Medium);
                    });
            });
        });

        return document.GeneratePdf();
    }

    // Gerar Atestado Médico
    public async Task<byte[]> GerarAtestadoAsync(int consultaId, string cid, int diasAfastamento, string observacoes)
    {
        var consulta = await _context.Consultas
            .Include(c => c.Paciente)
            .Include(c => c.Psicologo)
            .FirstOrDefaultAsync(c => c.Id == consultaId);

        if (consulta == null)
            throw new ArgumentException("Consulta não encontrada");

        var dataInicio = consulta.DataHorario.Date;
        var dataFim = dataInicio.AddDays(diasAfastamento - 1);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(12));

                page.Header()
                    .Column(col =>
                    {
                        col.Item().AlignCenter().Text("ATESTADO MÉDICO").FontSize(18).Bold();
                        col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                    });

                page.Content()
                    .PaddingTop(40)
                    .Column(col =>
                    {
                        col.Spacing(20);

                        // Texto do atestado
                        col.Item().Text(text =>
                        {
                            text.Span("Atesto para os devidos fins que ").FontSize(12);
                            text.Span(consulta.Paciente?.Nome ?? "").Bold().FontSize(12);
                            text.Span(", portador(a) do CPF ").FontSize(12);
                            text.Span(consulta.Paciente?.CPF ?? "").Bold().FontSize(12);
                            text.Span(", esteve sob meus cuidados profissionais no dia ").FontSize(12);
                            text.Span(consulta.DataHorario.ToString("dd/MM/yyyy")).Bold().FontSize(12);
                        });

                        if (!string.IsNullOrEmpty(cid))
                        {
                            col.Item().Text(text =>
                            {
                                text.Span("CID-10: ").FontSize(12);
                                text.Span(cid).Bold().FontSize(12);
                            });
                        }

                        col.Item().Text(text =>
                        {
                            text.Span("Necessitando de afastamento de suas atividades pelo período de ").FontSize(12);
                            text.Span($"{diasAfastamento} dia(s)").Bold().FontSize(12);
                            text.Span(", no período de ").FontSize(12);
                            text.Span(dataInicio.ToString("dd/MM/yyyy")).Bold().FontSize(12);
                            text.Span(" a ").FontSize(12);
                            text.Span(dataFim.ToString("dd/MM/yyyy")).Bold().FontSize(12);
                            text.Span(".").FontSize(12);
                        });

                        if (!string.IsNullOrEmpty(observacoes))
                        {
                            col.Item().PaddingTop(20).Text(text =>
                            {
                                text.Span("Observações: ").Bold().FontSize(12);
                                text.Span(observacoes).FontSize(12);
                            });
                        }

                        col.Item().PaddingTop(60).AlignCenter().Text(text =>
                        {
                            text.Span("São Paulo, ").FontSize(12);
                            text.Span(DateTime.Now.ToString("dd 'de' MMMM 'de' yyyy", new System.Globalization.CultureInfo("pt-BR"))).FontSize(12);
                        });

                        // Assinatura
                        col.Item().PaddingTop(60).AlignCenter().Column(signCol =>
                        {
                            signCol.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken3);
                            signCol.Item().PaddingTop(5).Text(consulta.Psicologo?.Nome ?? "").Bold().FontSize(12);
                            signCol.Item().Text($"CRP: {consulta.Psicologo?.CRP ?? ""}").FontSize(11);
                            signCol.Item().Text($"Email: {consulta.Psicologo?.Email ?? ""}").FontSize(10).FontColor(Colors.Grey.Darken2);
                            signCol.Item().Text($"Telefone: {consulta.Psicologo?.Telefone ?? ""}").FontSize(10).FontColor(Colors.Grey.Darken2);
                        });
                    });

                page.Footer()
                    .AlignCenter()
                    .Text(text =>
                    {
                        text.Span("Documento gerado eletronicamente em ").FontSize(8).FontColor(Colors.Grey.Medium);
                        text.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(8).FontColor(Colors.Grey.Medium);
                    });
            });
        });

        return document.GeneratePdf();
    }
    }

    public class ReceitaPorPsicologoDto
    {
        public string Nome { get; set; } = string.Empty;
        public int Consultas { get; set; }
        public decimal Receita { get; set; }
    }
}
