using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ClinicaPsi.Application.Services;
using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;

namespace ClinicaPsi.Web.Pages.Psicologo
{
    [Authorize(Roles = "Psicologo")]
    public class DocumentosModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly PdfService _pdfService;
        private readonly ConfiguracaoService _configuracaoService;

        public DocumentosModel(
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            PdfService pdfService,
            ConfiguracaoService configuracaoService)
        {
            _context = context;
            _userManager = userManager;
            _pdfService = pdfService;
            _configuracaoService = configuracaoService;
        }

        public List<Paciente> Pacientes { get; set; } = new();
        public decimal ValorConsultaPadrao { get; set; } = 150m;
        public string DescricaoPadrao { get; set; } = "Consulta psicológica";
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return RedirectToPage("/Account/Login");
                }

                var psicologo = await _context.Psicologos
                    .FirstOrDefaultAsync(p => p.UserId == user.Id);

                if (psicologo == null)
                {
                    ErrorMessage = "Psicólogo não encontrado no sistema.";
                    return Page();
                }

                Pacientes = await _context.Pacientes
                    .Where(p => p.Ativo)
                    .OrderBy(p => p.Nome)
                    .ToListAsync();

                ValorConsultaPadrao = psicologo.ValorConsulta > 0
                    ? psicologo.ValorConsulta
                    : await _configuracaoService.ObterValorDecimalAsync("Consultas.ValorPadrao", 150m);

                DescricaoPadrao = await _configuracaoService.ObterValorStringAsync(
                    "ReceitaSaude.DescricaoPadrao", "Consulta psicológica") ?? "Consulta psicológica";

                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Erro ao carregar pacientes: {ex.Message}";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostGerarDeclaracaoAsync(
            int pacienteId,
            string dataConsulta,
            string horaConsulta,
            int duracao)
        {
            try
            {
                var (psicologo, paciente, error) = await ResolverPsicologoEPacienteAsync(pacienteId);
                if (error != null) return error;
                if (!TryParseDataHora(dataConsulta, horaConsulta, out var dataHorario, out var parseError))
                {
                    ErrorMessage = parseError;
                    return await OnGetAsync();
                }

                var pdfBytes = await _pdfService.GerarDeclaracaoComparecimentoManualAsync(
                    paciente!,
                    psicologo!,
                    dataHorario,
                    duracao);

                var nomeArquivo = $"Declaracao_{SanitizeFileName(paciente!.Nome)}_{DateTime.Now:yyyyMMdd}.pdf";
                return File(pdfBytes, "application/pdf", nomeArquivo);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Erro ao gerar declaração: {ex.Message}";
                return await OnGetAsync();
            }
        }

        public async Task<IActionResult> OnPostGerarAtestadoAsync(
            int pacienteId,
            string dataConsulta,
            string horaConsulta,
            string? cid,
            int diasAfastamento,
            string? observacoes)
        {
            try
            {
                var (psicologo, paciente, error) = await ResolverPsicologoEPacienteAsync(pacienteId);
                if (error != null) return error;

                if (diasAfastamento < 1 || diasAfastamento > 90)
                {
                    ErrorMessage = "O período de afastamento deve ser entre 1 e 90 dias.";
                    return await OnGetAsync();
                }

                if (!TryParseDataHora(dataConsulta, horaConsulta, out var dataHorario, out var parseError))
                {
                    ErrorMessage = parseError;
                    return await OnGetAsync();
                }

                var pdfBytes = await _pdfService.GerarAtestadoManualAsync(
                    paciente!,
                    psicologo!,
                    dataHorario,
                    cid ?? string.Empty,
                    diasAfastamento,
                    observacoes ?? string.Empty);

                var nomeArquivo = $"Atestado_{SanitizeFileName(paciente!.Nome)}_{DateTime.Now:yyyyMMdd}.pdf";
                return File(pdfBytes, "application/pdf", nomeArquivo);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Erro ao gerar atestado: {ex.Message}";
                return await OnGetAsync();
            }
        }

        public async Task<IActionResult> OnPostGerarReciboSaudeAsync(
            int pacienteId,
            string dataPagamento,
            string? dataAtendimento,
            string valor,
            string? descricao,
            string? cpfPagador,
            string? cpfProfissional)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return RedirectToPage("/Account/Login");

                var (psicologo, paciente, error) = await ResolverPsicologoEPacienteAsync(pacienteId);
                if (error != null) return error;

                if (!DateTime.TryParse(dataPagamento, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtPagamento)
                    && !DateTime.TryParse(dataPagamento, CultureInfo.GetCultureInfo("pt-BR"), DateTimeStyles.None, out dtPagamento))
                {
                    ErrorMessage = "Data de pagamento inválida.";
                    return await OnGetAsync();
                }

                DateTime? dtAtendimento = null;
                if (!string.IsNullOrWhiteSpace(dataAtendimento))
                {
                    if (DateTime.TryParse(dataAtendimento, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                        || DateTime.TryParse(dataAtendimento, CultureInfo.GetCultureInfo("pt-BR"), DateTimeStyles.None, out parsed))
                    {
                        dtAtendimento = parsed.Date;
                    }
                }

                if (!TryParseValor(valor, out var valorDecimal))
                {
                    ErrorMessage = "Valor inválido.";
                    return await OnGetAsync();
                }

                if (valorDecimal <= 0)
                {
                    ErrorMessage = "O valor do recibo deve ser maior que zero.";
                    return await OnGetAsync();
                }

                var clinica = await _configuracaoService.ObterConfigSistemaAsync();
                var descPadrao = await _configuracaoService.ObterValorStringAsync(
                    "ReceitaSaude.DescricaoPadrao", "Consulta psicológica") ?? "Consulta psicológica";
                var cpfProfConfig = await _configuracaoService.ObterValorStringAsync(
                    "ReceitaSaude.CpfProfissionalPadrao", "") ?? string.Empty;

                var cpfProf = CarneLeaoEscrituracaoHelper.SomenteDigitos(
                    !string.IsNullOrWhiteSpace(cpfProfissional) ? cpfProfissional
                    : !string.IsNullOrWhiteSpace(user.CPF) ? user.CPF
                    : cpfProfConfig);

                var dados = new ReciboServicoSaudeDados
                {
                    Paciente = paciente!,
                    Psicologo = psicologo!,
                    Clinica = clinica,
                    DataPagamento = dtPagamento.Date,
                    DataAtendimento = dtAtendimento,
                    Valor = valorDecimal,
                    Descricao = string.IsNullOrWhiteSpace(descricao) ? descPadrao : descricao.Trim(),
                    CpfProfissional = string.IsNullOrWhiteSpace(cpfProf) ? null : cpfProf,
                    CpfPagador = string.IsNullOrWhiteSpace(cpfPagador) ? paciente!.CPF : cpfPagador
                };

                var pdfBytes = await _pdfService.GerarReciboServicosSaudeAsync(dados);
                var nomeArquivo = $"ReciboSaude_{SanitizeFileName(paciente!.Nome)}_{dtPagamento:yyyyMMdd}.pdf";
                return File(pdfBytes, "application/pdf", nomeArquivo);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Erro ao gerar recibo: {ex.Message}";
                return await OnGetAsync();
            }
        }

        public async Task<IActionResult> OnPostExportarCsvCarneLeaoAsync(
            int pacienteId,
            string dataPagamento,
            string? dataAtendimento,
            string valor,
            string? descricao,
            string? cpfPagador,
            string? cpfProfissional)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return RedirectToPage("/Account/Login");

                var (psicologo, paciente, error) = await ResolverPsicologoEPacienteAsync(pacienteId);
                if (error != null) return error;

                if (!DateTime.TryParse(dataPagamento, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtPagamento)
                    && !DateTime.TryParse(dataPagamento, CultureInfo.GetCultureInfo("pt-BR"), DateTimeStyles.None, out dtPagamento))
                {
                    ErrorMessage = "Data de pagamento inválida.";
                    return await OnGetAsync();
                }

                if (!TryParseValor(valor, out var valorDecimal) || valorDecimal <= 0)
                {
                    ErrorMessage = "Valor inválido.";
                    return await OnGetAsync();
                }

                var cpfProfConfig = await _configuracaoService.ObterValorStringAsync(
                    "ReceitaSaude.CpfProfissionalPadrao", "") ?? string.Empty;
                var cpfProf = CarneLeaoEscrituracaoHelper.SomenteDigitos(
                    !string.IsNullOrWhiteSpace(cpfProfissional) ? cpfProfissional
                    : !string.IsNullOrWhiteSpace(user.CPF) ? user.CPF
                    : cpfProfConfig);

                if (cpfProf.Length != 11)
                {
                    ErrorMessage = "Para o CSV do Carnê-Leão é obrigatório o CPF do profissional (11 dígitos). Informe no formulário, no perfil do usuário ou em Configurações → ReceitaSaude.CpfProfissionalPadrao.";
                    return await OnGetAsync();
                }

                var cpfPaciente = CarneLeaoEscrituracaoHelper.SomenteDigitos(paciente!.CPF);
                if (cpfPaciente.Length != 11)
                {
                    ErrorMessage = "O paciente precisa ter CPF válido (11 dígitos) para o CSV.";
                    return await OnGetAsync();
                }

                var cpfPag = CarneLeaoEscrituracaoHelper.SomenteDigitos(
                    string.IsNullOrWhiteSpace(cpfPagador) ? paciente.CPF : cpfPagador);
                if (cpfPag.Length != 11) cpfPag = cpfPaciente;

                var descPadrao = await _configuracaoService.ObterValorStringAsync(
                    "ReceitaSaude.DescricaoPadrao", "Consulta psicológica") ?? "Consulta psicológica";
                var desc = string.IsNullOrWhiteSpace(descricao) ? descPadrao : descricao.Trim();
                if (!string.IsNullOrWhiteSpace(dataAtendimento)
                    && (DateTime.TryParse(dataAtendimento, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtAt)
                        || DateTime.TryParse(dataAtendimento, CultureInfo.GetCultureInfo("pt-BR"), DateTimeStyles.None, out dtAt)))
                {
                    desc = $"{desc} (atendimento {dtAt:dd/MM/yyyy})";
                }

                var linha = CarneLeaoEscrituracaoHelper.MontarLinhaRecibo(
                    dtPagamento.Date,
                    valorDecimal,
                    desc,
                    cpfPag,
                    cpfPaciente,
                    cpfProf,
                    psicologo!.CRP);

                var bytes = CarneLeaoEscrituracaoHelper.GerarArquivoUtf8(new[] { linha });
                var nomeArquivo = $"CarneLeao_ReceitaSaude_{SanitizeFileName(paciente.Nome)}_{dtPagamento:yyyyMMdd}.csv";
                return File(bytes, "text/csv", nomeArquivo);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Erro ao exportar CSV: {ex.Message}";
                return await OnGetAsync();
            }
        }

        private async Task<(ClinicaPsi.Shared.Models.Psicologo? psicologo, Paciente? paciente, IActionResult? error)>
            ResolverPsicologoEPacienteAsync(int pacienteId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return (null, null, RedirectToPage("/Account/Login"));

            var psicologo = await _context.Psicologos
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            if (psicologo == null)
            {
                ErrorMessage = "Psicólogo não encontrado.";
                return (null, null, await OnGetAsync());
            }

            var paciente = await _context.Pacientes
                .FirstOrDefaultAsync(p => p.Id == pacienteId);

            if (paciente == null)
            {
                ErrorMessage = "Paciente não encontrado.";
                return (null, null, await OnGetAsync());
            }

            return (psicologo, paciente, null);
        }

        private static bool TryParseDataHora(string dataConsulta, string horaConsulta, out DateTime dataHorario, out string? error)
        {
            dataHorario = default;
            error = null;

            if (!DateTime.TryParse(dataConsulta, out var data))
            {
                error = "Data inválida.";
                return false;
            }

            if (!TimeSpan.TryParse(horaConsulta, out var hora))
            {
                error = "Horário inválido.";
                return false;
            }

            dataHorario = data.Date.Add(hora);
            return true;
        }

        private static bool TryParseValor(string? valor, out decimal result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(valor)) return false;

            if (decimal.TryParse(valor, NumberStyles.Number, CultureInfo.InvariantCulture, out result))
                return true;

            return decimal.TryParse(valor, NumberStyles.Number, CultureInfo.GetCultureInfo("pt-BR"), out result);
        }

        private static string SanitizeFileName(string nome) =>
            string.Join("_", nome.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries))
                .Replace(' ', '_');
    }
}
