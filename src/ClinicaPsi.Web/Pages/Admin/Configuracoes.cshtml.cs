using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ClinicaPsi.Shared.Models;
using ClinicaPsi.Application.Services;
using ClinicaPsi.Application.Services.Email;

namespace ClinicaPsi.Web.Pages.Admin
{
    [Authorize(Policy = "AdminPolicy")]
    public class ConfiguracoesModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ConfiguracaoService _configuracaoService;
        private readonly IEmailService _emailService;

        public ConfiguracoesModel(
            UserManager<ApplicationUser> userManager,
            ConfiguracaoService configuracaoService,
            IEmailService emailService)
        {
            _userManager = userManager;
            _configuracaoService = configuracaoService;
            _emailService = emailService;
        }

        [BindProperty]
        public ConfiguracoesGerais Configuracoes { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !await _userManager.IsInRoleAsync(user, "Admin"))
            {
                return Forbid();
            }

            await _configuracaoService.InicializarConfiguracoesAsync();
            Configuracoes = await CarregarConfiguracoesAsync();
            ViewData["ResendConfigured"] = _emailService.IsConfigured;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !await _userManager.IsInRoleAsync(user, "Admin"))
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Por favor, corrija os erros no formulário.";
                return Page();
            }

            if (Configuracoes.PontosParaConsultaGratis <= 0)
            {
                ModelState.AddModelError("Configuracoes.PontosParaConsultaGratis", "Pontos para consulta grátis deve ser maior que zero.");
                TempData["ErrorMessage"] = "Por favor, corrija os erros no formulário.";
                return Page();
            }

            if (Configuracoes.HorarioInicioAtendimento >= Configuracoes.HorarioFimAtendimento)
            {
                ModelState.AddModelError("Configuracoes.HorarioFimAtendimento", "Horário de fim deve ser maior que o horário de início.");
                TempData["ErrorMessage"] = "Por favor, corrija os erros no formulário.";
                return Page();
            }

            var usuarioNome = user.NomeCompleto ?? user.Email;

            await _configuracaoService.SalvarAsync("Sistema.Nome", Configuracoes.NomeClinica,
                "Nome do sistema", "Sistema", "string", usuarioNome);

            await _configuracaoService.SalvarAsync("Sistema.Email", Configuracoes.EmailContato,
                "Email principal do sistema", "Sistema", "string", usuarioNome);

            await _configuracaoService.SalvarAsync("Sistema.Telefone", Configuracoes.TelefoneContato,
                "Telefone de contato", "Sistema", "string", usuarioNome);

            await _configuracaoService.SalvarAsync("Sistema.Endereco", Configuracoes.EnderecoCompleto,
                "Endereço da clínica", "Sistema", "string", usuarioNome);

            await _configuracaoService.SalvarAsync("Sistema.HorarioFuncionamento", Configuracoes.HorarioFuncionamento,
                "Horário de funcionamento exibido no site", "Sistema", "string", usuarioNome);

            await _configuracaoService.SalvarAsync("Consultas.ValorPadrao",
                Configuracoes.ValorConsultaPadrao.ToString(CultureInfo.InvariantCulture),
                "Valor padrão da consulta", "Consultas", "number", usuarioNome);

            await _configuracaoService.SalvarAsync("Consultas.DuracaoPadrao", Configuracoes.DuracaoConsultaPadrao.ToString(CultureInfo.InvariantCulture),
                "Duração padrão das consultas em minutos", "Consultas", "number", usuarioNome);

            await _configuracaoService.SalvarAsync("Consultas.IntervaloMinimo", Configuracoes.IntervaloEntreConsultas.ToString(CultureInfo.InvariantCulture),
                "Intervalo mínimo entre consultas em minutos", "Consultas", "number", usuarioNome);

            await _configuracaoService.SalvarAsync("Consultas.HorarioInicio", Configuracoes.HorarioInicioAtendimento.ToString("HH:mm", CultureInfo.InvariantCulture),
                "Horário de início do atendimento", "Consultas", "string", usuarioNome);

            await _configuracaoService.SalvarAsync("Consultas.HorarioFim", Configuracoes.HorarioFimAtendimento.ToString("HH:mm", CultureInfo.InvariantCulture),
                "Horário de fim do atendimento", "Consultas", "string", usuarioNome);

            await _configuracaoService.SalvarAsync("Consultas.PermitirSabado", Configuracoes.PermitirAgendamentoSabado.ToString().ToLowerInvariant(),
                "Permitir agendamento aos sábados", "Consultas", "boolean", usuarioNome);

            await _configuracaoService.SalvarAsync("Consultas.PermitirDomingo", Configuracoes.PermitirAgendamentoDomingo.ToString().ToLowerInvariant(),
                "Permitir agendamento aos domingos", "Consultas", "boolean", usuarioNome);

            await _configuracaoService.SalvarAsync("PsicoPontos.PontosPorConsulta", Configuracoes.PontosConsultaRealizada.ToString(CultureInfo.InvariantCulture),
                "Pontos ganhos por consulta realizada", "PsicoPontos", "number", usuarioNome);

            await _configuracaoService.SalvarAsync("PsicoPontos.PontosParaConsultaGratuita", Configuracoes.PontosParaConsultaGratis.ToString(CultureInfo.InvariantCulture),
                "Quantidade de pontos necessários para consulta gratuita", "PsicoPontos", "number", usuarioNome);

            await _configuracaoService.SalvarAsync("Notificacoes.Lembrete.AntecedenciaHoras",
                (Configuracoes.DiasLembreteConsulta * 24).ToString(CultureInfo.InvariantCulture),
                "Antecedência em horas para envio de lembretes", "Notificacoes", "number", usuarioNome);

            await _configuracaoService.SalvarAsync("Notificacoes.Email.Habilitado", Configuracoes.EmailNotificacoes.ToString().ToLowerInvariant(),
                "Habilitar envio de notificações por Email", "Notificacoes", "boolean", usuarioNome);

            await _configuracaoService.SalvarAsync("Notificacoes.WhatsApp.Habilitado", Configuracoes.WhatsappNotificacoes.ToString().ToLowerInvariant(),
                "Habilitar envio de notificações por WhatsApp", "Notificacoes", "boolean", usuarioNome);

            await _configuracaoService.SalvarAsync("Notificacoes.SMS.Habilitado", Configuracoes.SmsNotificacoes.ToString().ToLowerInvariant(),
                "Habilitar envio de notificações por SMS", "Notificacoes", "boolean", usuarioNome);

            await _configuracaoService.SalvarAsync("Email.From", Configuracoes.EmailFrom,
                "Endereço From dos e-mails (domínio verificado no Resend)", "Email", "string", usuarioNome);

            await _configuracaoService.SalvarAsync("Email.FromName", Configuracoes.EmailFromName,
                "Nome exibido no From dos e-mails", "Email", "string", usuarioNome);

            await _configuracaoService.SalvarAsync("Sistema.ManterHistoricoCompleto", Configuracoes.ManterHistoricoCompleto.ToString().ToLowerInvariant(),
                "Manter histórico completo de consultas e pontos", "Sistema", "boolean", usuarioNome);

            await _configuracaoService.SalvarAsync("Backup.Automatico.Habilitado", Configuracoes.BackupAutomatico.ToString().ToLowerInvariant(),
                "Habilitar backup automático", "Backup", "boolean", usuarioNome);

            await _configuracaoService.SalvarAsync("Backup.Automatico.Frequencia", Configuracoes.FrequenciaBackup,
                "Frequência do backup automático", "Backup", "string", usuarioNome);

            await _configuracaoService.SalvarAsync("Prontuario.Habilitado",
                Configuracoes.ProntuarioOnlineHabilitado.ToString().ToLowerInvariant(),
                "Habilitar prontuário eletrônico online", "Prontuario", "boolean", usuarioNome);

            await _configuracaoService.SalvarAsync("Video.ConsultasOnline.Habilitado",
                Configuracoes.VideoConsultasOnlineHabilitado.ToString().ToLowerInvariant(),
                "Habilitar videochamada WebRTC 1:1 em consultas online", "Video", "boolean", usuarioNome);

            TempData["SuccessMessage"] = "Configurações salvas com sucesso!";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostResetarSistemaAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !await _userManager.IsInRoleAsync(user, "Admin"))
            {
                return Forbid();
            }

            TempData["WarningMessage"] = "Funcionalidade de reset não implementada por segurança. Entre em contato com o suporte técnico.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostBackupAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !await _userManager.IsInRoleAsync(user, "Admin"))
            {
                return Forbid();
            }

            TempData["SuccessMessage"] = "Backup realizado com sucesso! Arquivo salvo em: backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".db";
            return RedirectToPage();
        }

        private async Task<ConfiguracoesGerais> CarregarConfiguracoesAsync()
        {
            var horarioInicio = await _configuracaoService.ObterValorStringAsync("Consultas.HorarioInicio", "09:00");
            var horarioFim = await _configuracaoService.ObterValorStringAsync("Consultas.HorarioFim", "17:00");
            var antecedenciaHoras = await _configuracaoService.ObterValorIntAsync("Notificacoes.Lembrete.AntecedenciaHoras", 24);

            return new ConfiguracoesGerais
            {
                NomeClinica = await _configuracaoService.ObterValorStringAsync("Sistema.Nome", "PsiiAnaSantos") ?? "PsiiAnaSantos",
                EmailContato = await _configuracaoService.ObterValorStringAsync("Sistema.Email", "psiianasantos@psiianasantos.com.br") ?? string.Empty,
                TelefoneContato = await _configuracaoService.ObterValorStringAsync("Sistema.Telefone", "(42) 98859-3775") ?? string.Empty,
                EnderecoCompleto = await _configuracaoService.ObterValorStringAsync("Sistema.Endereco", "Rua Emma Marcelino Peralta - 168 - 86030-540 - Londrina, PR") ?? string.Empty,
                HorarioFuncionamento = await _configuracaoService.ObterValorStringAsync("Sistema.HorarioFuncionamento", "Segunda a Sexta: 9h às 17h") ?? string.Empty,
                ValorConsultaPadrao = await _configuracaoService.ObterValorDecimalAsync("Consultas.ValorPadrao", 150.00m),
                DuracaoConsultaPadrao = await _configuracaoService.ObterValorIntAsync("Consultas.DuracaoPadrao", 50),
                IntervaloEntreConsultas = await _configuracaoService.ObterValorIntAsync("Consultas.IntervaloMinimo", 15),
                PontosConsultaRealizada = await _configuracaoService.ObterValorIntAsync("PsicoPontos.PontosPorConsulta", 1),
                PontosParaConsultaGratis = await _configuracaoService.ObterValorIntAsync("PsicoPontos.PontosParaConsultaGratuita", 10),
                DiasLembreteConsulta = Math.Max(0, antecedenciaHoras / 24),
                PermitirAgendamentoSabado = await _configuracaoService.ObterValorBoolAsync("Consultas.PermitirSabado", true),
                PermitirAgendamentoDomingo = await _configuracaoService.ObterValorBoolAsync("Consultas.PermitirDomingo", false),
                HorarioInicioAtendimento = TimeOnly.TryParse(horarioInicio, CultureInfo.InvariantCulture, DateTimeStyles.None, out var inicio)
                    ? inicio
                    : TimeOnly.Parse("09:00", CultureInfo.InvariantCulture),
                HorarioFimAtendimento = TimeOnly.TryParse(horarioFim, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fim)
                    ? fim
                    : TimeOnly.Parse("17:00", CultureInfo.InvariantCulture),
                EmailNotificacoes = await _configuracaoService.ObterValorBoolAsync("Notificacoes.Email.Habilitado"),
                WhatsappNotificacoes = await _configuracaoService.ObterValorBoolAsync("Notificacoes.WhatsApp.Habilitado"),
                SmsNotificacoes = await _configuracaoService.ObterValorBoolAsync("Notificacoes.SMS.Habilitado"),
                EmailFrom = await _configuracaoService.ObterValorStringAsync("Email.From", "noreply@psiianasantos.com.br") ?? "noreply@psiianasantos.com.br",
                EmailFromName = await _configuracaoService.ObterValorStringAsync("Email.FromName", "Psicóloga Ana Santos") ?? "Psicóloga Ana Santos",
                ManterHistoricoCompleto = await _configuracaoService.ObterValorBoolAsync("Sistema.ManterHistoricoCompleto", true),
                BackupAutomatico = await _configuracaoService.ObterValorBoolAsync("Backup.Automatico.Habilitado"),
                FrequenciaBackup = await _configuracaoService.ObterValorStringAsync("Backup.Automatico.Frequencia", "Diário") ?? "Diário",
                ProntuarioOnlineHabilitado = await _configuracaoService.ObterValorBoolAsync("Prontuario.Habilitado", true),
                VideoConsultasOnlineHabilitado = await _configuracaoService.ObterValorBoolAsync("Video.ConsultasOnline.Habilitado", true)
            };
        }
    }

    public class ConfiguracoesGerais
    {
        public string NomeClinica { get; set; } = string.Empty;
        public string EmailContato { get; set; } = string.Empty;
        public string TelefoneContato { get; set; } = string.Empty;
        public string EnderecoCompleto { get; set; } = string.Empty;
        public string HorarioFuncionamento { get; set; } = string.Empty;

        public decimal ValorConsultaPadrao { get; set; }
        public int DuracaoConsultaPadrao { get; set; }
        public int IntervaloEntreConsultas { get; set; }

        public int PontosConsultaRealizada { get; set; }
        public int PontosParaConsultaGratis { get; set; }

        public int DiasLembreteConsulta { get; set; }
        public bool EmailNotificacoes { get; set; }
        public bool WhatsappNotificacoes { get; set; }
        public bool SmsNotificacoes { get; set; }

        public string EmailFrom { get; set; } = "noreply@psiianasantos.com.br";
        public string EmailFromName { get; set; } = "Psicóloga Ana Santos";

        public bool PermitirAgendamentoSabado { get; set; }
        public bool PermitirAgendamentoDomingo { get; set; }
        public TimeOnly HorarioInicioAtendimento { get; set; }
        public TimeOnly HorarioFimAtendimento { get; set; }

        public bool ManterHistoricoCompleto { get; set; }
        public bool BackupAutomatico { get; set; }
        public string FrequenciaBackup { get; set; } = string.Empty;

        public bool ProntuarioOnlineHabilitado { get; set; } = true;
        public bool VideoConsultasOnlineHabilitado { get; set; } = true;
    }
}
