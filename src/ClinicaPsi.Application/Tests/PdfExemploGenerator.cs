using ClinicaPsi.Application.Services;
using ClinicaPsi.Shared.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ClinicaPsi.Application.Tests;

/// <summary>
/// Classe utilitária para gerar PDF de exemplo com dados fictícios
/// </summary>
public static class PdfExemploGenerator
{
    public static byte[] GerarPdfExemplo()
    {
        // Configurar licença
        QuestPDF.Settings.License = LicenseType.Community;

        // Criar dados fictícios
        var paciente = new Paciente
        {
            Id = 1,
            Nome = "Maria Silva Santos",
            CPF = "12345678901",
            Email = "maria.silva@email.com",
            Telefone = "(42) 99999-8888",
            PsicoPontos = 7,
            ConsultasGratuitas = 0,
            ConsultasRealizadas = 12
        };

        var consultas = new List<Consulta>
        {
            new Consulta
            {
                Id = 1,
                DataHorario = DateTime.Now.AddDays(-90),
                Tipo = TipoConsulta.Avaliacao,
                Status = StatusConsulta.Realizada,
                Valor = 150.00m,
                Psicologo = new Psicologo { Nome = "Psi. Ana Santos" }
            },
            new Consulta
            {
                Id = 2,
                DataHorario = DateTime.Now.AddDays(-83),
                Tipo = TipoConsulta.Normal,
                Status = StatusConsulta.Realizada,
                Valor = 150.00m,
                Psicologo = new Psicologo { Nome = "Psi. Ana Santos" }
            },
            new Consulta
            {
                Id = 3,
                DataHorario = DateTime.Now.AddDays(-76),
                Tipo = TipoConsulta.Normal,
                Status = StatusConsulta.Realizada,
                Valor = 150.00m,
                Psicologo = new Psicologo { Nome = "Psi. Ana Santos" }
            },
            new Consulta
            {
                Id = 4,
                DataHorario = DateTime.Now.AddDays(-69),
                Tipo = TipoConsulta.Normal,
                Status = StatusConsulta.Realizada,
                Valor = 150.00m,
                Psicologo = new Psicologo { Nome = "Psi. Ana Santos" }
            },
            new Consulta
            {
                Id = 5,
                DataHorario = DateTime.Now.AddDays(-62),
                Tipo = TipoConsulta.Normal,
                Status = StatusConsulta.Realizada,
                Valor = 150.00m,
                Psicologo = new Psicologo { Nome = "Psi. Ana Santos" }
            },
            new Consulta
            {
                Id = 6,
                DataHorario = DateTime.Now.AddDays(-55),
                Tipo = TipoConsulta.Normal,
                Status = StatusConsulta.Realizada,
                Valor = 150.00m,
                Psicologo = new Psicologo { Nome = "Psi. Ana Santos" }
            },
            new Consulta
            {
                Id = 7,
                DataHorario = DateTime.Now.AddDays(-48),
                Tipo = TipoConsulta.Normal,
                Status = StatusConsulta.Realizada,
                Valor = 150.00m,
                Psicologo = new Psicologo { Nome = "Psi. Ana Santos" }
            },
            new Consulta
            {
                Id = 8,
                DataHorario = DateTime.Now.AddDays(-41),
                Tipo = TipoConsulta.Normal,
                Status = StatusConsulta.Cancelada,
                Valor = 150.00m,
                Psicologo = new Psicologo { Nome = "Psi. Ana Santos" }
            },
            new Consulta
            {
                Id = 9,
                DataHorario = DateTime.Now.AddDays(-34),
                Tipo = TipoConsulta.Normal,
                Status = StatusConsulta.Realizada,
                Valor = 150.00m,
                Psicologo = new Psicologo { Nome = "Psi. Ana Santos" }
            },
            new Consulta
            {
                Id = 10,
                DataHorario = DateTime.Now.AddDays(-27),
                Tipo = TipoConsulta.Normal,
                Status = StatusConsulta.Realizada,
                Valor = 150.00m,
                Psicologo = new Psicologo { Nome = "Psi. Ana Santos" }
            },
            new Consulta
            {
                Id = 11,
                DataHorario = DateTime.Now.AddDays(-20),
                Tipo = TipoConsulta.Normal,
                Status = StatusConsulta.Realizada,
                Valor = 150.00m,
                Psicologo = new Psicologo { Nome = "Psi. Ana Santos" }
            },
            new Consulta
            {
                Id = 12,
                DataHorario = DateTime.Now.AddDays(-13),
                Tipo = TipoConsulta.Normal,
                Status = StatusConsulta.Realizada,
                Valor = 150.00m,
                Psicologo = new Psicologo { Nome = "Psi. Ana Santos" }
            },
            new Consulta
            {
                Id = 13,
                DataHorario = DateTime.Now.AddDays(-6),
                Tipo = TipoConsulta.Normal,
                Status = StatusConsulta.Realizada,
                Valor = 150.00m,
                Psicologo = new Psicologo { Nome = "Psi. Ana Santos" }
            }
        };

        // Gerar o documento
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Element(c => ComposeHeader(c));
                page.Content().Element(c => ComposeContent(c, paciente, consultas));
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

    private static void ComposeHeader(IContainer container)
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
                column.Item().Text(text => text.Span("psiianasantos@psiianasantos.com.br").FontSize(9));
                column.Item().Text(text =>
                {
                    text.Span("Data: ").Bold().FontSize(9);
                    text.Span(DateTime.Now.ToString("dd/MM/yyyy")).FontSize(9);
                });
            });
        });
    }

    private static void ComposeContent(IContainer container, Paciente paciente, List<Consulta> consultas)
    {
        container.PaddingVertical(20).Column(column =>
        {
            column.Spacing(10);

            // Título
            column.Item().Text("HISTÓRICO DE CONSULTAS - EXEMPLO").FontSize(16).Bold().FontColor(Colors.Green.Medium);
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
                        text.Span(paciente.Email);
                    });
                });

                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(text =>
                    {
                        text.Span("Telefone: ").Bold();
                        text.Span(paciente.Telefone);
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

            // Estatísticas
            var totalConsultas = consultas.Count;
            var realizadas = consultas.Count(c => c.Status == StatusConsulta.Realizada);
            var canceladas = consultas.Count(c => c.Status == StatusConsulta.Cancelada);
            var valorTotal = consultas.Where(c => c.Status == StatusConsulta.Realizada).Sum(c => c.Valor);

            column.Item().Row(row =>
            {
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

            // Tabela
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(80);
                    columns.RelativeColumn();
                    columns.ConstantColumn(70);
                    columns.ConstantColumn(70);
                    columns.ConstantColumn(70);
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

            // Box PsicoPontos
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

            // Nota
            column.Item().PaddingTop(20).Background(Colors.Blue.Lighten5).Padding(10).Column(col =>
            {
                col.Item().Text("ℹ️ ESTE É UM PDF DE EXEMPLO").FontSize(11).Bold().FontColor(Colors.Blue.Medium);
                col.Item().Text("Este documento foi gerado automaticamente com dados fictícios para demonstração do sistema.").FontSize(9);
                col.Item().Text("Para gerar seu histórico real, faça login no sistema e acesse 'Meu Histórico > Exportar PDF'.").FontSize(9);
            });
        });
    }
}
