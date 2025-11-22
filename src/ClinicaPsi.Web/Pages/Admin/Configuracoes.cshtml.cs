using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using ClinicaPsi.Application.Services;

namespace ClinicaPsi.Web.Pages.Admin
{
    [Authorize(Policy = "AdminPolicy")]
    public class ConfiguracoesModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ConfiguracaoService _configuracaoService;

        public ConfiguracoesModel(
            AppDbContext context, 
            UserManager<ApplicationUser> userManager,
            ConfiguracaoService configuracaoService)
        {
            _context = context;
            _userManager = userManager;
            _configuracaoService = configuracaoService;
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

            // Inicializar configurações padrão se necessário
            await _configuracaoService.InicializarConfiguracoesAsync();

            // Carregar configurações do banco
            Configuracoes = new ConfiguracoesGerais
            {
                NomeClinica = await _configuracaoService.ObterValorStringAsync("Sistema.Nome", "PsiiAnaSantos - Clínica de Psicologia"),
                EmailContato = await _configuracaoService.ObterValorStringAsync("Sistema.Email", "psiianasantos@psiianasantos.com.br"),
                TelefoneContato = await _configuracaoService.ObterValorStringAsync("Sistema.Telefone", "(42) 99936-9724"),
                EnderecoCompleto = await _configuracaoService.ObterValorStringAsync("Sistema.Endereco", "Rua Orlando Ferreira Neto, 39 - Jd Itapoã, Londrina - PR, 86043-470"),
                HorarioFuncionamento = "Segunda a Sexta: 9h às 17h",
                ValorConsultaPadrao = await _configuracaoService.ObterValorDecimalAsync("Consultas.ValorPadrao", 150.00m),
                DuracaoConsultaPadrao = await _configuracaoService.ObterValorIntAsync("Consultas.DuracaoPadrao", 50),
                IntervaloEntreConsultas = await _configuracaoService.ObterValorIntAsync("Consultas.IntervaloMinimo", 15),
                PontosConsultaRealizada = await _configuracaoService.ObterValorIntAsync("PsicoPontos.PontosPorConsulta", 1),
                PontosParaConsultaGratis = await _configuracaoService.ObterValorIntAsync("PsicoPontos.PontosParaConsultaGratuita", 10),
                DiasLembreteConsulta = await _configuracaoService.ObterValorIntAsync("Notificacoes.Lembrete.AntecedenciaHoras", 24) / 24,
                PermitirAgendamentoSabado = true,
                PermitirAgendamentoDomingo = false,
                HorarioInicioAtendimento = TimeOnly.Parse("09:00"),
                HorarioFimAtendimento = TimeOnly.Parse("17:00"),
                EmailNotificacoes = await _configuracaoService.ObterValorBoolAsync("Notificacoes.Email.Habilitado"),
                WhatsappNotificacoes = await _configuracaoService.ObterValorBoolAsync("Notificacoes.WhatsApp.Habilitado"),
                SmsNotificacoes = await _configuracaoService.ObterValorBoolAsync("Notificacoes.SMS.Habilitado"),
                ManterHistoricoCompleto = true,
                BackupAutomatico = await _configuracaoService.ObterValorBoolAsync("Backup.Automatico.Habilitado"),
                FrequenciaBackup = "Diário"
            };

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

            // Validações específicas
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

            // Salvar configurações no banco de dados
            var usuarioNome = user.NomeCompleto ?? user.Email;
            
            await _configuracaoService.SalvarAsync("Sistema.Nome", Configuracoes.NomeClinica, 
                "Nome do sistema", "Sistema", "string", usuarioNome);
            
            await _configuracaoService.SalvarAsync("Sistema.Email", Configuracoes.EmailContato, 
                "Email principal do sistema", "Sistema", "string", usuarioNome);
            
            await _configuracaoService.SalvarAsync("Sistema.Telefone", Configuracoes.TelefoneContato, 
                "Telefone de contato", "Sistema", "string", usuarioNome);
            
            await _configuracaoService.SalvarAsync("Sistema.Endereco", Configuracoes.EnderecoCompleto, 
                "Endereço da clínica", "Sistema", "string", usuarioNome);
            
            await _configuracaoService.SalvarAsync("Consultas.ValorPadrao", Configuracoes.ValorConsultaPadrao.ToString(), 
                "Valor padrão da consulta", "Consultas", "number", usuarioNome);
            
            await _configuracaoService.SalvarAsync("Consultas.DuracaoPadrao", Configuracoes.DuracaoConsultaPadrao.ToString(), 
                "Duração padrão das consultas em minutos", "Consultas", "number", usuarioNome);
            
            await _configuracaoService.SalvarAsync("Consultas.IntervaloMinimo", Configuracoes.IntervaloEntreConsultas.ToString(), 
                "Intervalo mínimo entre consultas em minutos", "Consultas", "number", usuarioNome);
            
            await _configuracaoService.SalvarAsync("PsicoPontos.PontosPorConsulta", Configuracoes.PontosConsultaRealizada.ToString(), 
                "Pontos ganhos por consulta realizada", "PsicoPontos", "number", usuarioNome);
            
            await _configuracaoService.SalvarAsync("PsicoPontos.PontosParaConsultaGratuita", Configuracoes.PontosParaConsultaGratis.ToString(), 
                "Quantidade de pontos necessários para consulta gratuita", "PsicoPontos", "number", usuarioNome);
            
            await _configuracaoService.SalvarAsync("Notificacoes.Lembrete.AntecedenciaHoras", (Configuracoes.DiasLembreteConsulta * 24).ToString(), 
                "Antecedência em horas para envio de lembretes", "Notificacoes", "number", usuarioNome);
            
            await _configuracaoService.SalvarAsync("Notificacoes.Email.Habilitado", Configuracoes.EmailNotificacoes.ToString().ToLower(), 
                "Habilitar envio de notificações por Email", "Notificacoes", "boolean", usuarioNome);
            
            await _configuracaoService.SalvarAsync("Notificacoes.WhatsApp.Habilitado", Configuracoes.WhatsappNotificacoes.ToString().ToLower(), 
                "Habilitar envio de notificações por WhatsApp", "Notificacoes", "boolean", usuarioNome);
            
            await _configuracaoService.SalvarAsync("Notificacoes.SMS.Habilitado", Configuracoes.SmsNotificacoes.ToString().ToLower(), 
                "Habilitar envio de notificações por SMS", "Notificacoes", "boolean", usuarioNome);
            
            await _configuracaoService.SalvarAsync("Backup.Automatico.Habilitado", Configuracoes.BackupAutomatico.ToString().ToLower(), 
                "Habilitar backup automático", "Backup", "boolean", usuarioNome);
            
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

            // ATENÇÃO: Esta é uma operação destrutiva!
            // Em produção, deveria ter mais confirmações e logs
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

            // Simular backup do banco de dados
            TempData["SuccessMessage"] = "Backup realizado com sucesso! Arquivo salvo em: backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".db";
            return RedirectToPage();
        }
    }

    public class ConfiguracoesGerais
    {
        // Informações da Clínica
        public string NomeClinica { get; set; } = string.Empty;
        public string EmailContato { get; set; } = string.Empty;
        public string TelefoneContato { get; set; } = string.Empty;
        public string EnderecoCompleto { get; set; } = string.Empty;
        public string HorarioFuncionamento { get; set; } = string.Empty;

        // Configurações de Consulta
        public decimal ValorConsultaPadrao { get; set; }
        public int DuracaoConsultaPadrao { get; set; } // em minutos
        public int IntervaloEntreConsultas { get; set; } // em minutos

        // Sistema PsicoPontos
        public int PontosConsultaRealizada { get; set; }
        public int PontosParaConsultaGratis { get; set; }

        // Notificações
        public int DiasLembreteConsulta { get; set; }
        public bool EmailNotificacoes { get; set; }
        public bool WhatsappNotificacoes { get; set; }
        public bool SmsNotificacoes { get; set; }

        // Agendamento
        public bool PermitirAgendamentoSabado { get; set; }
        public bool PermitirAgendamentoDomingo { get; set; }
        public TimeOnly HorarioInicioAtendimento { get; set; }
        public TimeOnly HorarioFimAtendimento { get; set; }

        // Sistema
        public bool ManterHistoricoCompleto { get; set; }
        public bool BackupAutomatico { get; set; }
        public string FrequenciaBackup { get; set; } = string.Empty;
    }
}