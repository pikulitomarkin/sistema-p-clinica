using System.Globalization;
using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ClinicaPsi.Application.Services;

/// <summary>
/// Serviço para gerenciar configurações do sistema
/// </summary>
public class ConfiguracaoService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ConfiguracaoService> _logger;

    public ConfiguracaoService(AppDbContext context, ILogger<ConfiguracaoService> logger)
    {
        _context = context;
        _logger = logger;
    }

    #region Métodos Principais

    public async Task<ConfiguracaoSistema?> ObterPorChaveAsync(string chave)
    {
        return await _context.ConfiguracoesSistema
            .FirstOrDefaultAsync(c => c.Chave == chave);
    }

    public async Task<List<ConfiguracaoSistema>> ObterTodasAsync()
    {
        return await _context.ConfiguracoesSistema
            .OrderBy(c => c.Categoria)
            .ThenBy(c => c.Chave)
            .ToListAsync();
    }

    public async Task<List<ConfiguracaoSistema>> ObterPorCategoriaAsync(string categoria)
    {
        return await _context.ConfiguracoesSistema
            .Where(c => c.Categoria == categoria)
            .OrderBy(c => c.Chave)
            .ToListAsync();
    }

    public async Task<ConfiguracaoSistema> SalvarAsync(string chave, string? valor, string? descricao = null, 
        string? categoria = null, string tipoValor = "string", string? usuarioAtualizacao = null)
    {
        var config = await ObterPorChaveAsync(chave);

        if (config == null)
        {
            config = new ConfiguracaoSistema
            {
                Chave = chave,
                Valor = valor,
                Descricao = descricao,
                Categoria = categoria,
                TipoValor = tipoValor,
                DataCriacao = DateTime.Now,
                DataAtualizacao = DateTime.Now,
                UsuarioAtualizacao = usuarioAtualizacao
            };

            _context.ConfiguracoesSistema.Add(config);
            _logger.LogInformation("Nova configuração criada: {Chave}", chave);
        }
        else
        {
            config.Valor = valor;
            config.DataAtualizacao = DateTime.Now;
            config.UsuarioAtualizacao = usuarioAtualizacao;

            if (descricao != null) config.Descricao = descricao;
            if (categoria != null) config.Categoria = categoria;
            if (tipoValor != null) config.TipoValor = tipoValor;

            _logger.LogInformation("Configuração atualizada: {Chave}", chave);
        }

        await _context.SaveChangesAsync();
        return config;
    }

    public async Task<bool> ExcluirAsync(string chave)
    {
        var config = await ObterPorChaveAsync(chave);
        if (config == null) return false;

        _context.ConfiguracoesSistema.Remove(config);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Configuração excluída: {Chave}", chave);
        return true;
    }

    public async Task<bool> RemoverAsync(string chave)
    {
        return await ExcluirAsync(chave);
    }

    #endregion

    #region Métodos de Acesso Tipado

    public async Task<string?> ObterValorStringAsync(string chave, string? valorPadrao = null)
    {
        var config = await ObterPorChaveAsync(chave);
        return config?.Valor ?? valorPadrao;
    }

    public async Task<bool> ObterValorBoolAsync(string chave, bool valorPadrao = false)
    {
        var config = await ObterPorChaveAsync(chave);
        if (config?.Valor == null) return valorPadrao;

        return bool.TryParse(config.Valor, out var result) ? result : valorPadrao;
    }

    public async Task<int> ObterValorIntAsync(string chave, int valorPadrao = 0)
    {
        var config = await ObterPorChaveAsync(chave);
        if (config?.Valor == null) return valorPadrao;

        return int.TryParse(config.Valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : valorPadrao;
    }

    public async Task<decimal> ObterValorDecimalAsync(string chave, decimal valorPadrao = 0)
    {
        var config = await ObterPorChaveAsync(chave);
        if (config?.Valor == null) return valorPadrao;

        if (decimal.TryParse(config.Valor, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariant))
            return invariant;

        if (decimal.TryParse(config.Valor, NumberStyles.Number, CultureInfo.GetCultureInfo("pt-BR"), out var ptBr))
            return ptBr;

        return valorPadrao;
    }

    public async Task<T?> ObterValorJsonAsync<T>(string chave) where T : class
    {
        var config = await ObterPorChaveAsync(chave);
        if (config?.Valor == null) return null;

        try
        {
            return JsonSerializer.Deserialize<T>(config.Valor);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao deserializar configuração {Chave}", chave);
            return null;
        }
    }

    #endregion

    #region Configurações Específicas do Sistema

    public async Task InicializarConfiguracoesAsync()
    {
        var configuracoesPadrao = new Dictionary<string, (string valor, string descricao, string categoria, string tipo)>
        {
            { "Notificacoes.WhatsApp.Habilitado", ("false", "Habilitar envio de notificações por WhatsApp", "Notificacoes", "boolean") },
            { "Notificacoes.Email.Habilitado", ("false", "Habilitar envio de notificações por Email", "Notificacoes", "boolean") },
            { "Notificacoes.SMS.Habilitado", ("false", "Habilitar envio de notificações por SMS", "Notificacoes", "boolean") },
            { "Notificacoes.Lembrete.AntecedenciaHoras", ("24", "Antecedência em horas para envio de lembretes", "Notificacoes", "number") },
            
            { "Sistema.Nome", ("PsiiAnaSantos", "Nome do sistema", "Sistema", "string") },
            { "Sistema.Email", ("psiianasantos@psiianasantos.com.br", "Email principal do sistema", "Sistema", "string") },
            { "Sistema.Telefone", ("(42) 98859-3775", "Telefone de contato", "Sistema", "string") },
            { "Sistema.Endereco", ("Rua Emma Marcelino Peralta - 168 - 86030-540 - Londrina, PR", "Endereço da clínica", "Sistema", "string") },
            { "Sistema.HorarioFuncionamento", ("Segunda a Sexta: 9h às 17h", "Horário de funcionamento exibido no site", "Sistema", "string") },
            { "Sistema.ManterHistoricoCompleto", ("true", "Manter histórico completo de consultas", "Sistema", "boolean") },
            
            { "Consultas.DuracaoPadrao", ("50", "Duração padrão das consultas em minutos", "Consultas", "number") },
            { "Consultas.IntervaloMinimo", ("15", "Intervalo mínimo entre consultas em minutos", "Consultas", "number") },
            { "Consultas.ValorPadrao", ("150.00", "Valor padrão da consulta", "Consultas", "number") },
            { "Consultas.HorarioInicio", ("09:00", "Horário de início do atendimento", "Consultas", "string") },
            { "Consultas.HorarioFim", ("17:00", "Horário de fim do atendimento", "Consultas", "string") },
            { "Consultas.PermitirSabado", ("true", "Permitir agendamento aos sábados", "Consultas", "boolean") },
            { "Consultas.PermitirDomingo", ("false", "Permitir agendamento aos domingos", "Consultas", "boolean") },
            
            { "Backup.Automatico.Habilitado", ("false", "Habilitar backup automático", "Backup", "boolean") },
            { "Backup.Automatico.Horario", ("02:00", "Horário do backup automático (HH:mm)", "Backup", "string") },
            { "Backup.Automatico.Frequencia", ("Diário", "Frequência do backup automático", "Backup", "string") },
            { "Backup.Automatico.DiasRetencao", ("30", "Dias de retenção dos backups", "Backup", "number") },
            
            { "Seguranca.SessaoTimeout", ("30", "Tempo de expiração da sessão em minutos", "Seguranca", "number") },
            { "Seguranca.TentativasLoginMax", ("5", "Número máximo de tentativas de login", "Seguranca", "number") },

            { "Prontuario.Habilitado", ("true", "Habilitar prontuário eletrônico online", "Prontuario", "boolean") },
            { "Video.ConsultasOnline.Habilitado", ("true", "Habilitar videochamada WebRTC 1:1 em consultas online", "Video", "boolean") },
            { "Video.Provider", ("webrtc", "Provedor de vídeo: webrtc (SignalR 1:1, sem conta externa). Opcional: daily se DAILY_API_KEY no ambiente", "Video", "string") },
            { "Email.From", ("noreply@psiianasantos.com.br", "Endereço From dos e-mails (use domínio verificado no Resend quando disponível)", "Email", "string") },
            { "Email.FromName", ("Psicóloga Ana Santos", "Nome exibido no From dos e-mails", "Email", "string") }
        };

        foreach (var (chave, (valor, descricao, categoria, tipo)) in configuracoesPadrao)
        {
            var existe = await ObterPorChaveAsync(chave);
            if (existe == null)
            {
                await SalvarAsync(chave, valor, descricao, categoria, tipo, "Sistema");
            }
        }

        _logger.LogInformation("Configurações padrão inicializadas");
    }

    public async Task<NotificacoesConfig> ObterConfigNotificacoesAsync()
    {
        return new NotificacoesConfig
        {
            WhatsAppHabilitado = await ObterValorBoolAsync("Notificacoes.WhatsApp.Habilitado"),
            EmailHabilitado = await ObterValorBoolAsync("Notificacoes.Email.Habilitado"),
            SmsHabilitado = await ObterValorBoolAsync("Notificacoes.SMS.Habilitado"),
            AntecedenciaHoras = await ObterValorIntAsync("Notificacoes.Lembrete.AntecedenciaHoras", 24)
        };
    }

    public async Task<SistemaConfig> ObterConfigSistemaAsync()
    {
        return new SistemaConfig
        {
            Nome = await ObterValorStringAsync("Sistema.Nome", "PsiiAnaSantos") ?? "PsiiAnaSantos",
            Email = await ObterValorStringAsync("Sistema.Email", "psiianasantos@psiianasantos.com.br") ?? string.Empty,
            Telefone = await ObterValorStringAsync("Sistema.Telefone", "(42) 98859-3775") ?? string.Empty,
            Endereco = await ObterValorStringAsync("Sistema.Endereco", "") ?? string.Empty,
            HorarioFuncionamento = await ObterValorStringAsync("Sistema.HorarioFuncionamento", "Segunda a Sexta: 9h às 17h") ?? string.Empty
        };
    }

    public async Task<ConsultasConfig> ObterConfigConsultasAsync()
    {
        return new ConsultasConfig
        {
            ValorPadrao = await ObterValorDecimalAsync("Consultas.ValorPadrao", 150.00m),
            DuracaoPadrao = await ObterValorIntAsync("Consultas.DuracaoPadrao", 50),
            IntervaloMinimo = await ObterValorIntAsync("Consultas.IntervaloMinimo", 15),
            HorarioInicio = await ObterValorStringAsync("Consultas.HorarioInicio", "09:00") ?? "09:00",
            HorarioFim = await ObterValorStringAsync("Consultas.HorarioFim", "17:00") ?? "17:00",
            PermitirSabado = await ObterValorBoolAsync("Consultas.PermitirSabado", true),
            PermitirDomingo = await ObterValorBoolAsync("Consultas.PermitirDomingo", false)
        };
    }

    #endregion
}

#region Classes de Configuração

public class NotificacoesConfig
{
    public bool WhatsAppHabilitado { get; set; }
    public bool EmailHabilitado { get; set; }
    public bool SmsHabilitado { get; set; }
    public int AntecedenciaHoras { get; set; } = 24;
}

public class SistemaConfig
{
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public string Endereco { get; set; } = string.Empty;
    public string HorarioFuncionamento { get; set; } = string.Empty;
}

public class ConsultasConfig
{
    public decimal ValorPadrao { get; set; } = 150.00m;
    public int DuracaoPadrao { get; set; } = 50;
    public int IntervaloMinimo { get; set; } = 15;
    public string HorarioInicio { get; set; } = "09:00";
    public string HorarioFim { get; set; } = "17:00";
    public bool PermitirSabado { get; set; } = true;
    public bool PermitirDomingo { get; set; }
}

#endregion
