
using System;
using System.IO;
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
        private const string ASSINATURA_PATH = "wwwroot/images/assinaturas/assinatura-psicologo.png";

        public PdfService(AppDbContext context)
        {
            _context = context;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        // Método auxiliar para verificar se existe assinatura
        private byte[]? ObterAssinaturaPsicologo(string? crp = null)
        {
            try
            {
                // Tenta buscar assinatura específica do psicólogo (futuro)
                if (!string.IsNullOrEmpty(crp))
                {
                    var assinaturaEspecifica = $"wwwroot/images/assinaturas/assinatura-{crp.Replace("/", "-")}.png";
                    if (File.Exists(assinaturaEspecifica))
                    {
                        return File.ReadAllBytes(assinaturaEspecifica);
                    }
                }

                // Usa assinatura padrão
                if (File.Exists(ASSINATURA_PATH))
                {
                    return File.ReadAllBytes(ASSINATURA_PATH);
                }

                return null;
            }
            catch
            {
                return null;
            }
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
                        col.Item().Background(Colors.Green.Lighten4).Padding(15).Column(headerCol =>
                        {
                            headerCol.Item().AlignCenter().Text("DECLARAÇÃO DE COMPARECIMENTO")
                                .FontSize(20).Bold().FontColor(Colors.Green.Darken2);
                            headerCol.Item().PaddingTop(5).AlignCenter().Text("Atendimento Psicológico")
                                .FontSize(11).FontColor(Colors.Grey.Darken1);
                        });
                        col.Item().PaddingTop(5).LineHorizontal(3).LineColor(Colors.Green.Medium);
                    });

                page.Content()
                    .PaddingTop(30)
                    .Column(col =>
                    {
                        col.Spacing(15);

                        // Texto da declaração com estilo moderno
                        col.Item().Background(Colors.Grey.Lighten5).Padding(20).Text(text =>
                        {
                            text.Span("Declaro para os devidos fins que ").FontSize(12).LineHeight(1.5f);
                            text.Span(consulta.Paciente?.Nome ?? "").Bold().FontSize(12).FontColor(Colors.Blue.Darken2);
                            text.Span(", portador(a) do CPF ").FontSize(12);
                            text.Span(consulta.Paciente?.CPF ?? "").Bold().FontSize(12).FontColor(Colors.Blue.Darken2);
                            text.Span(", compareceu à consulta psicológica no dia ").FontSize(12);
                            text.Span(consulta.DataHorario.ToString("dd/MM/yyyy")).Bold().FontSize(12).FontColor(Colors.Blue.Darken2);
                            text.Span(" às ").FontSize(12);
                            text.Span(consulta.DataHorario.ToString("HH:mm")).Bold().FontSize(12).FontColor(Colors.Blue.Darken2);
                            text.Span(" com duração de ").FontSize(12);
                            text.Span($"{consulta.DuracaoMinutos} minutos").Bold().FontSize(12).FontColor(Colors.Blue.Darken2);
                            text.Span(".").FontSize(12);
                        });

                        col.Item().PaddingTop(50).AlignCenter().Text(text =>
                        {
                            text.Span("Londrina - PR, ").FontSize(12);
                            text.Span(DateTime.Now.ToString("dd 'de' MMMM 'de' yyyy", new System.Globalization.CultureInfo("pt-BR"))).FontSize(12);
                        });

                        // Assinatura moderna com imagem
                        col.Item().PaddingTop(50).AlignCenter().Column(signCol =>
                        {
                            // Tentar adicionar imagem da assinatura
                            var assinaturaBytes = ObterAssinaturaPsicologo(consulta.Psicologo?.CRP);
                            if (assinaturaBytes != null)
                            {
                                signCol.Item().Width(150).Image(assinaturaBytes);
                            }
                            
                            signCol.Item().Width(200).LineHorizontal(2).LineColor(Colors.Green.Medium);
                            signCol.Item().PaddingTop(8).Text(consulta.Psicologo?.Nome ?? "").Bold().FontSize(13).FontColor(Colors.Green.Darken2);
                            signCol.Item().PaddingTop(2).Text($"CRP: {consulta.Psicologo?.CRP ?? ""}").FontSize(11).FontColor(Colors.Grey.Darken2);
                            signCol.Item().PaddingTop(5).Text($"✉ {consulta.Psicologo?.Email ?? ""}").FontSize(9).FontColor(Colors.Blue.Medium);
                            signCol.Item().Text($"☎ {consulta.Psicologo?.Telefone ?? ""}").FontSize(9).FontColor(Colors.Blue.Medium);
                        });
                    });

                page.Footer()
                    .Padding(10)
                    .AlignCenter()
                    .Column(footerCol =>
                    {
                        footerCol.Item().LineHorizontal(1).LineColor(Colors.Green.Lighten2);
                        footerCol.Item().PaddingTop(5).Text(text =>
                        {
                            text.Span("Documento gerado eletronicamente em ").FontSize(8).FontColor(Colors.Grey.Medium);
                            text.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
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
                        col.Item().Background(Colors.Red.Lighten4).Padding(15).Column(headerCol =>
                        {
                            headerCol.Item().AlignCenter().Text("ATESTADO MÉDICO")
                                .FontSize(20).Bold().FontColor(Colors.Red.Darken2);
                            headerCol.Item().PaddingTop(5).AlignCenter().Text("Atendimento em Saúde Mental")
                                .FontSize(11).FontColor(Colors.Grey.Darken1);
                        });
                        col.Item().PaddingTop(5).LineHorizontal(3).LineColor(Colors.Red.Medium);
                    });

                page.Content()
                    .PaddingTop(30)
                    .Column(col =>
                    {
                        col.Spacing(15);

                        // Texto do atestado com estilo moderno
                        col.Item().Background(Colors.Grey.Lighten5).Padding(20).Column(textCol =>
                        {
                            textCol.Item().Text(text =>
                            {
                                text.Span("Atesto para os devidos fins que ").FontSize(12).LineHeight(1.5f);
                                text.Span(consulta.Paciente?.Nome ?? "").Bold().FontSize(12).FontColor(Colors.Blue.Darken2);
                                text.Span(", portador(a) do CPF ").FontSize(12);
                                text.Span(consulta.Paciente?.CPF ?? "").Bold().FontSize(12).FontColor(Colors.Blue.Darken2);
                                text.Span(", esteve sob meus cuidados profissionais no dia ").FontSize(12);
                                text.Span(consulta.DataHorario.ToString("dd/MM/yyyy")).Bold().FontSize(12).FontColor(Colors.Blue.Darken2);
                                text.Span(".").FontSize(12);
                            });

                            if (!string.IsNullOrEmpty(cid))
                            {
                                textCol.Item().PaddingTop(10).Text(text =>
                                {
                                    text.Span("CID-10: ").FontSize(12).Bold();
                                    text.Span(cid).Bold().FontSize(12).FontColor(Colors.Red.Darken1);
                                });
                            }

                            textCol.Item().PaddingTop(10).Text(text =>
                            {
                                text.Span("Necessitando de afastamento de suas atividades pelo período de ").FontSize(12).LineHeight(1.5f);
                                text.Span($"{diasAfastamento} dia(s)").Bold().FontSize(12).FontColor(Colors.Red.Darken1);
                                text.Span(", no período de ").FontSize(12);
                                text.Span(dataInicio.ToString("dd/MM/yyyy")).Bold().FontSize(12).FontColor(Colors.Blue.Darken2);
                                text.Span(" a ").FontSize(12);
                                text.Span(dataFim.ToString("dd/MM/yyyy")).Bold().FontSize(12).FontColor(Colors.Blue.Darken2);
                                text.Span(".").FontSize(12);
                            });

                            if (!string.IsNullOrEmpty(observacoes))
                            {
                                textCol.Item().PaddingTop(15).Text(text =>
                                {
                                    text.Span("Observações: ").Bold().FontSize(12);
                                    text.Span(observacoes).FontSize(12).LineHeight(1.4f);
                                });
                            }
                        });

                        col.Item().PaddingTop(50).AlignCenter().Text(text =>
                        {
                            text.Span("Londrina - PR, ").FontSize(12);
                            text.Span(DateTime.Now.ToString("dd 'de' MMMM 'de' yyyy", new System.Globalization.CultureInfo("pt-BR"))).FontSize(12);
                        });

                        // Assinatura moderna com imagem
                        col.Item().PaddingTop(50).AlignCenter().Column(signCol =>
                        {
                            var assinaturaBytes = ObterAssinaturaPsicologo(consulta.Psicologo?.CRP);
                            if (assinaturaBytes != null)
                            {
                                signCol.Item().Width(150).Image(assinaturaBytes);
                            }
                            
                            signCol.Item().Width(200).LineHorizontal(2).LineColor(Colors.Red.Medium);
                            signCol.Item().PaddingTop(8).Text(consulta.Psicologo?.Nome ?? "").Bold().FontSize(13).FontColor(Colors.Red.Darken2);
                            signCol.Item().PaddingTop(2).Text($"CRP: {consulta.Psicologo?.CRP ?? ""}").FontSize(11).FontColor(Colors.Grey.Darken2);
                            signCol.Item().PaddingTop(5).Text($"✉ {consulta.Psicologo?.Email ?? ""}").FontSize(9).FontColor(Colors.Blue.Medium);
                            signCol.Item().Text($"☎ {consulta.Psicologo?.Telefone ?? ""}").FontSize(9).FontColor(Colors.Blue.Medium);
                        });
                    });

                page.Footer()
                    .Padding(10)
                    .AlignCenter()
                    .Column(footerCol =>
                    {
                        footerCol.Item().LineHorizontal(1).LineColor(Colors.Red.Lighten2);
                        footerCol.Item().PaddingTop(5).Text(text =>
                        {
                            text.Span("Documento gerado eletronicamente em ").FontSize(8).FontColor(Colors.Grey.Medium);
                            text.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                    });
            });
        });

        return document.GeneratePdf();
    }

    // Gerar Declaração de Comparecimento - Versão Manual (sem consulta no sistema)
    public Task<byte[]> GerarDeclaracaoComparecimentoManualAsync(
        Paciente paciente,
        ClinicaPsi.Shared.Models.Psicologo psicologo,
        DateTime dataHorario,
        int duracaoMinutos)
    {
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
                        col.Item().Background(Colors.Green.Lighten4).Padding(15).Column(headerCol =>
                        {
                            headerCol.Item().AlignCenter().Text("DECLARAÇÃO DE COMPARECIMENTO")
                                .FontSize(20).Bold().FontColor(Colors.Green.Darken2);
                            headerCol.Item().PaddingTop(5).AlignCenter().Text("Atendimento Psicológico")
                                .FontSize(11).FontColor(Colors.Grey.Darken1);
                        });
                        col.Item().PaddingTop(5).LineHorizontal(3).LineColor(Colors.Green.Medium);
                    });

                page.Content()
                    .PaddingTop(30)
                    .Column(col =>
                    {
                        col.Spacing(15);

                        // Texto da declaração com estilo moderno
                        col.Item().Background(Colors.Grey.Lighten5).Padding(20).Text(text =>
                        {
                            text.Span("Declaro para os devidos fins que ").FontSize(12).LineHeight(1.5f);
                            text.Span(paciente.Nome).Bold().FontSize(12).FontColor(Colors.Blue.Darken2);
                            text.Span(", portador(a) do CPF ").FontSize(12);
                            text.Span(paciente.CPF).Bold().FontSize(12).FontColor(Colors.Blue.Darken2);
                            text.Span(", compareceu à consulta psicológica no dia ").FontSize(12);
                            text.Span(dataHorario.ToString("dd/MM/yyyy")).Bold().FontSize(12).FontColor(Colors.Blue.Darken2);
                            text.Span(" às ").FontSize(12);
                            text.Span(dataHorario.ToString("HH:mm")).Bold().FontSize(12).FontColor(Colors.Blue.Darken2);
                            text.Span(" com duração de ").FontSize(12);
                            text.Span($"{duracaoMinutos} minutos").Bold().FontSize(12).FontColor(Colors.Blue.Darken2);
                            text.Span(".").FontSize(12);
                        });

                        col.Item().PaddingTop(50).AlignCenter().Text(text =>
                        {
                            text.Span("Londrina - PR, ").FontSize(12);
                            text.Span(DateTime.Now.ToString("dd 'de' MMMM 'de' yyyy", new System.Globalization.CultureInfo("pt-BR"))).FontSize(12);
                        });

                        // Assinatura moderna com imagem
                        col.Item().PaddingTop(50).AlignCenter().Column(signCol =>
                        {
                            var assinaturaBytes = ObterAssinaturaPsicologo(psicologo.CRP);
                            if (assinaturaBytes != null)
                            {
                                signCol.Item().Width(150).Image(assinaturaBytes);
                            }
                            
                            signCol.Item().Width(200).LineHorizontal(2).LineColor(Colors.Green.Medium);
                            signCol.Item().PaddingTop(8).Text(psicologo.Nome).Bold().FontSize(13).FontColor(Colors.Green.Darken2);
                            signCol.Item().PaddingTop(2).Text($"CRP: {psicologo.CRP}").FontSize(11).FontColor(Colors.Grey.Darken2);
                            signCol.Item().PaddingTop(5).Text($"✉ {psicologo.Email}").FontSize(9).FontColor(Colors.Blue.Medium);
                            signCol.Item().Text($"☎ {psicologo.Telefone}").FontSize(9).FontColor(Colors.Blue.Medium);
                        });
                    });

                page.Footer()
                    .Padding(10)
                    .AlignCenter()
                    .Column(footerCol =>
                    {
                        footerCol.Item().LineHorizontal(1).LineColor(Colors.Green.Lighten2);
                        footerCol.Item().PaddingTop(5).Text(text =>
                        {
                            text.Span("Documento gerado eletronicamente em ").FontSize(8).FontColor(Colors.Grey.Medium);
                            text.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                    });
            });
        });

        return Task.FromResult(document.GeneratePdf());
    }

    // Gerar Atestado - Versão Manual (sem consulta no sistema)
    public Task<byte[]> GerarAtestadoManualAsync(
        Paciente paciente,
        ClinicaPsi.Shared.Models.Psicologo psicologo,
        DateTime dataHorario,
        string cid,
        int diasAfastamento,
        string observacoes)
    {
        var dataInicio = dataHorario.Date;
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
                        col.Item().Background(Colors.Red.Lighten4).Padding(15).Column(headerCol =>
                        {
                            headerCol.Item().AlignCenter().Text("ATESTADO MÉDICO")
                                .FontSize(20).Bold().FontColor(Colors.Red.Darken2);
                            headerCol.Item().PaddingTop(5).AlignCenter().Text("Atendimento em Saúde Mental")
                                .FontSize(11).FontColor(Colors.Grey.Darken1);
                        });
                        col.Item().PaddingTop(5).LineHorizontal(3).LineColor(Colors.Red.Medium);
                    });

                page.Content()
                    .PaddingTop(30)
                    .Column(col =>
                    {
                        col.Spacing(15);

                        // Texto do atestado com estilo moderno
                        col.Item().Background(Colors.Grey.Lighten5).Padding(20).Column(textCol =>
                        {
                            textCol.Item().Text(text =>
                            {
                                text.Span("Atesto para os devidos fins que ").FontSize(12).LineHeight(1.5f);
                                text.Span(paciente.Nome).Bold().FontSize(12).FontColor(Colors.Blue.Darken2);
                                text.Span(", portador(a) do CPF ").FontSize(12);
                                text.Span(paciente.CPF).Bold().FontSize(12).FontColor(Colors.Blue.Darken2);
                                text.Span(", esteve sob meus cuidados profissionais no dia ").FontSize(12);
                                text.Span(dataHorario.ToString("dd/MM/yyyy")).Bold().FontSize(12).FontColor(Colors.Blue.Darken2);
                                text.Span(".").FontSize(12);
                            });

                            if (!string.IsNullOrEmpty(cid))
                            {
                                textCol.Item().PaddingTop(10).Text(text =>
                                {
                                    text.Span("CID-10: ").FontSize(12).Bold();
                                    text.Span(cid).Bold().FontSize(12).FontColor(Colors.Red.Darken1);
                                });
                            }

                            textCol.Item().PaddingTop(10).Text(text =>
                            {
                                text.Span("Necessitando de afastamento de suas atividades pelo período de ").FontSize(12).LineHeight(1.5f);
                                text.Span($"{diasAfastamento} dia(s)").Bold().FontSize(12).FontColor(Colors.Red.Darken1);
                                text.Span(", no período de ").FontSize(12);
                                text.Span(dataInicio.ToString("dd/MM/yyyy")).Bold().FontSize(12).FontColor(Colors.Blue.Darken2);
                                text.Span(" a ").FontSize(12);
                                text.Span(dataFim.ToString("dd/MM/yyyy")).Bold().FontSize(12).FontColor(Colors.Blue.Darken2);
                                text.Span(".").FontSize(12);
                            });

                            if (!string.IsNullOrEmpty(observacoes))
                            {
                                textCol.Item().PaddingTop(15).Text(text =>
                                {
                                    text.Span("Observações: ").Bold().FontSize(12);
                                    text.Span(observacoes).FontSize(12).LineHeight(1.4f);
                                });
                            }
                        });

                        col.Item().PaddingTop(50).AlignCenter().Text(text =>
                        {
                            text.Span("Londrina - PR, ").FontSize(12);
                            text.Span(DateTime.Now.ToString("dd 'de' MMMM 'de' yyyy", new System.Globalization.CultureInfo("pt-BR"))).FontSize(12);
                        });

                        // Assinatura moderna com imagem
                        col.Item().PaddingTop(50).AlignCenter().Column(signCol =>
                        {
                            var assinaturaBytes = ObterAssinaturaPsicologo(psicologo.CRP);
                            if (assinaturaBytes != null)
                            {
                                signCol.Item().Width(150).Image(assinaturaBytes);
                            }
                            
                            signCol.Item().Width(200).LineHorizontal(2).LineColor(Colors.Red.Medium);
                            signCol.Item().PaddingTop(8).Text(psicologo.Nome).Bold().FontSize(13).FontColor(Colors.Red.Darken2);
                            signCol.Item().PaddingTop(2).Text($"CRP: {psicologo.CRP}").FontSize(11).FontColor(Colors.Grey.Darken2);
                            signCol.Item().PaddingTop(5).Text($"✉ {psicologo.Email}").FontSize(9).FontColor(Colors.Blue.Medium);
                            signCol.Item().Text($"☎ {psicologo.Telefone}").FontSize(9).FontColor(Colors.Blue.Medium);
                        });
                    });

                page.Footer()
                    .Padding(10)
                    .AlignCenter()
                    .Column(footerCol =>
                    {
                        footerCol.Item().LineHorizontal(1).LineColor(Colors.Red.Lighten2);
                        footerCol.Item().PaddingTop(5).Text(text =>
                        {
                            text.Span("Documento gerado eletronicamente em ").FontSize(8).FontColor(Colors.Grey.Medium);
                            text.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                    });
            });
        });

        return Task.FromResult(document.GeneratePdf());
    }

        /// <summary>
        /// Recibo interno de prestação de serviços de saúde (PDF).
        /// Não substitui o Receita Saúde oficial (App RFB / Carnê-Leão).
        /// </summary>
        public Task<byte[]> GerarReciboServicosSaudeAsync(ReciboServicoSaudeDados dados)
        {
            var cultura = new System.Globalization.CultureInfo("pt-BR");
            var cpfPaciente = CarneLeaoEscrituracaoHelper.FormatCpf(dados.Paciente.CPF);
            var cpfPagador = CarneLeaoEscrituracaoHelper.FormatCpf(
                string.IsNullOrWhiteSpace(dados.CpfPagador) ? dados.Paciente.CPF : dados.CpfPagador);
            var cpfProf = string.IsNullOrWhiteSpace(dados.CpfProfissional)
                ? null
                : CarneLeaoEscrituracaoHelper.FormatCpf(dados.CpfProfissional);
            var dataAtend = dados.DataAtendimento ?? dados.DataPagamento;
            var numero = string.IsNullOrWhiteSpace(dados.NumeroRecibo)
                ? $"RS-{dados.DataPagamento:yyyyMMdd}-{dados.Paciente.Id:D4}"
                : dados.NumeroRecibo;

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
                            col.Item().Background(Colors.Teal.Lighten4).Padding(15).Column(headerCol =>
                            {
                                headerCol.Item().AlignCenter().Text("RECIBO DE PRESTAÇÃO DE SERVIÇOS DE SAÚDE")
                                    .FontSize(16).Bold().FontColor(Colors.Teal.Darken2);
                                headerCol.Item().PaddingTop(4).AlignCenter().Text(dados.Clinica.Nome)
                                    .FontSize(12).FontColor(Colors.Grey.Darken2);
                                headerCol.Item().PaddingTop(2).AlignCenter().Text($"Nº {numero}")
                                    .FontSize(10).FontColor(Colors.Grey.Darken1);
                            });
                            col.Item().PaddingTop(5).LineHorizontal(3).LineColor(Colors.Teal.Medium);
                        });

                    page.Content()
                        .PaddingTop(20)
                        .Column(col =>
                        {
                            col.Spacing(12);

                            if (!string.IsNullOrWhiteSpace(dados.Clinica.Endereco) ||
                                !string.IsNullOrWhiteSpace(dados.Clinica.Telefone) ||
                                !string.IsNullOrWhiteSpace(dados.Clinica.Email))
                            {
                                col.Item().Text(text =>
                                {
                                    if (!string.IsNullOrWhiteSpace(dados.Clinica.Endereco))
                                        text.Span(dados.Clinica.Endereco).FontSize(9).FontColor(Colors.Grey.Darken1);
                                    if (!string.IsNullOrWhiteSpace(dados.Clinica.Telefone))
                                        text.Span($"  |  Tel: {dados.Clinica.Telefone}").FontSize(9).FontColor(Colors.Grey.Darken1);
                                    if (!string.IsNullOrWhiteSpace(dados.Clinica.Email))
                                        text.Span($"  |  {dados.Clinica.Email}").FontSize(9).FontColor(Colors.Grey.Darken1);
                                });
                            }

                            col.Item().Background(Colors.Grey.Lighten5).Padding(16).Column(box =>
                            {
                                box.Item().Text("Recebi de:").FontSize(10).FontColor(Colors.Grey.Darken1);
                                box.Item().PaddingTop(4).Text(dados.Paciente.Nome).Bold().FontSize(13);
                                box.Item().Text($"CPF do paciente/beneficiário: {cpfPaciente}").FontSize(11);
                                if (!string.Equals(
                                        CarneLeaoEscrituracaoHelper.SomenteDigitos(cpfPagador),
                                        CarneLeaoEscrituracaoHelper.SomenteDigitos(dados.Paciente.CPF),
                                        StringComparison.Ordinal))
                                {
                                    box.Item().Text($"CPF do pagador: {cpfPagador}").FontSize(11);
                                }

                                box.Item().PaddingTop(12).Text("A importância de:").FontSize(10).FontColor(Colors.Grey.Darken1);
                                box.Item().PaddingTop(4).Text(dados.Valor.ToString("C", cultura))
                                    .Bold().FontSize(18).FontColor(Colors.Teal.Darken2);

                                box.Item().PaddingTop(12).Text("Referente a:").FontSize(10).FontColor(Colors.Grey.Darken1);
                                box.Item().PaddingTop(4).Text(dados.Descricao).FontSize(12);
                                box.Item().PaddingTop(6).Text(
                                    $"Data do atendimento: {dataAtend:dd/MM/yyyy}  |  Data do pagamento: {dados.DataPagamento:dd/MM/yyyy}")
                                    .FontSize(11);
                            });

                            col.Item().PaddingTop(8).Text("Prestador do serviço").Bold().FontSize(11);
                            col.Item().Text($"{dados.Psicologo.Nome}  —  CRP {dados.Psicologo.CRP}").FontSize(12);
                            if (cpfProf != null)
                                col.Item().Text($"CPF do profissional: {cpfProf}").FontSize(11);
                            if (!string.IsNullOrWhiteSpace(dados.Psicologo.Email))
                                col.Item().Text(dados.Psicologo.Email).FontSize(10).FontColor(Colors.Grey.Darken1);

                            col.Item().PaddingTop(30).AlignCenter().Text(text =>
                            {
                                text.Span("Londrina - PR, ").FontSize(11);
                                text.Span(DateTime.Now.ToString("dd 'de' MMMM 'de' yyyy", cultura)).FontSize(11);
                            });

                            col.Item().PaddingTop(40).AlignCenter().Column(signCol =>
                            {
                                var assinaturaBytes = ObterAssinaturaPsicologo(dados.Psicologo.CRP);
                                if (assinaturaBytes != null)
                                    signCol.Item().Width(150).Image(assinaturaBytes);

                                signCol.Item().Width(220).LineHorizontal(2).LineColor(Colors.Teal.Medium);
                                signCol.Item().PaddingTop(8).Text(dados.Psicologo.Nome).Bold().FontSize(12)
                                    .FontColor(Colors.Teal.Darken2);
                                signCol.Item().Text($"CRP: {dados.Psicologo.CRP}").FontSize(10);
                            });

                            col.Item().PaddingTop(24).Background(Colors.Amber.Lighten4).Padding(10).Column(aviso =>
                            {
                                aviso.Item().Text("Aviso importante").Bold().FontSize(9).FontColor(Colors.Amber.Darken3);
                                aviso.Item().PaddingTop(3).Text(
                                    "Este PDF é um comprovante interno gerado pelo ClinicaPsi. " +
                                    "Para fins de Imposto de Renda (dedução de despesas médicas e Carnê-Leão), " +
                                    "o recibo oficial Receita Saúde deve ser emitido no App Receita Federal ou no Carnê-Leão Web " +
                                    "(profissional pessoa física). Este documento não substitui a emissão oficial.")
                                    .FontSize(8).FontColor(Colors.Grey.Darken2).LineHeight(1.3f);
                            });
                        });

                    page.Footer()
                        .Padding(8)
                        .AlignCenter()
                        .Column(footerCol =>
                        {
                            footerCol.Item().LineHorizontal(1).LineColor(Colors.Teal.Lighten2);
                            footerCol.Item().PaddingTop(4).Text(
                                    $"Documento gerado em {DateTime.Now:dd/MM/yyyy HH:mm} — ClinicaPsi")
                                .FontSize(8).FontColor(Colors.Grey.Medium);
                        });
                });
            });

            return Task.FromResult(document.GeneratePdf());
        }
    }

    public class ReceitaPorPsicologoDto
    {
        public string Nome { get; set; } = string.Empty;
        public int Consultas { get; set; }
        public decimal Receita { get; set; }
    }
}
