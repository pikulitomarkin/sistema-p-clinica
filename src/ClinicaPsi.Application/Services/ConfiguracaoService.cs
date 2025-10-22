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

    /// <summary>
    /// Obtém uma configuração por chave
    /// </summary>
    public async Task<ConfiguracaoSistema?> ObterPorChaveAsync(string chave)
    {
        return await _context.ConfiguracoesSistema
            .FirstOrDefaultAsync(c => c.Chave == chave);
    }

    /// <summary>
    /// Obtém todas as configurações
    /// </summary>
    public async Task<List<ConfiguracaoSistema>> ObterTodasAsync()
    {
        return await _context.ConfiguracoesSistema
            .OrderBy(c => c.Categoria)
            .ThenBy(c => c.Chave)
            .ToListAsync();
    }

    /// <summary>
    /// Obtém configurações por categoria
    /// </summary>
    public async Task<List<ConfiguracaoSistema>> ObterPorCategoriaAsync(string categoria)
    {
        return await _context.ConfiguracoesSistema
            .Where(c => c.Categoria == categoria)
            .OrderBy(c => c.Chave)
            .ToListAsync();
    }

    /// <summary>
    /// Salva ou atualiza uma configuração
    /// </summary>
    public async Task<ConfiguracaoSistema> SalvarAsync(string chave, string? valor, string? descricao = null, 
        string? categoria = null, string tipoValor = "string", string? usuarioAtualizacao = null)
    {
        var config = await ObterPorChaveAsync(chave);

        if (config == null)
        {
            // Criar nova configuração
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
            _logger.LogInformation($"Nova configuração criada: {chave}");
        }
        else
        {
            // Atualizar configuração existente
            config.Valor = valor;
            config.DataAtualizacao = DateTime.Now;
            config.UsuarioAtualizacao = usuarioAtualizacao;

            if (descricao != null) config.Descricao = descricao;
            if (categoria != null) config.Categoria = categoria;
            if (tipoValor != null) config.TipoValor = tipoValor;

            _logger.LogInformation($"Configuração atualizada: {chave}");
        }

        await _context.SaveChangesAsync();
        return config;
    }

    /// <summary>
    /// Exclui uma configuração
    /// </summary>
    public async Task<bool> ExcluirAsync(string chave)
    {
        var config = await ObterPorChaveAsync(chave);
        if (config == null) return false;

        _context.ConfiguracoesSistema.Remove(config);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Configuração excluída: {chave}");
        return true;
    }

    #endregion

    #region Métodos de Acesso Tipado

    /// <summary>
    /// Obtém valor como string
    /// </summary>
    public async Task<string?> ObterValorStringAsync(string chave, string? valorPadrao = null)
    {
        var config = await ObterPorChaveAsync(chave);
        return config?.Valor ?? valorPadrao;
    }

    /// <summary>
    /// Obtém valor como boolean
    /// </summary>
    public async Task<bool> ObterValorBoolAsync(string chave, bool valorPadrao = false)
    {
        var config = await ObterPorChaveAsync(chave);
        if (config?.Valor == null) return valorPadrao;

        return bool.TryParse(config.Valor, out var result) ? result : valorPadrao;
    }

    /// <summary>
    /// Obtém valor como int
    /// </summary>
    public async Task<int> ObterValorIntAsync(string chave, int valorPadrao = 0)
    {
        var config = await ObterPorChaveAsync(chave);
        if (config?.Valor == null) return valorPadrao;

        return int.TryParse(config.Valor, out var result) ? result : valorPadrao;
    }

    /// <summary>
    /// Obtém valor como decimal
    /// </summary>
    public async Task<decimal> ObterValorDecimalAsync(string chave, decimal valorPadrao = 0)
    {
        var config = await ObterPorChaveAsync(chave);
        if (config?.Valor == null) return valorPadrao;

        return decimal.TryParse(config.Valor, out var result) ? result : valorPadrao;
    }

    /// <summary>
    /// Obtém valor como objeto JSON
    /// </summary>
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
            _logger.LogError(ex, $"Erro ao deserializar configuração {chave}");
            return null;
        }
    }

    #endregion

    #region Configurações Específicas do Sistema

    /// <summary>
    /// Inicializa configurações padrão do sistema
    /// </summary>
    public async Task InicializarConfiguracoesAsync()
    {
        var configuracoesPadrao = new Dictionary<string, (string valor, string descricao, string categoria, string tipo)>
        {
            // Notificações
            { "Notificacoes.WhatsApp.Habilitado", ("false", "Habilitar envio de notificações por WhatsApp", "Notificacoes", "boolean") },
            { "Notificacoes.Email.Habilitado", ("false", "Habilitar envio de notificações por Email", "Notificacoes", "boolean") },
            { "Notificacoes.SMS.Habilitado", ("false", "Habilitar envio de notificações por SMS", "Notificacoes", "boolean") },
            { "Notificacoes.Lembrete.AntecedenciaHoras", ("24", "Antecedência em horas para envio de lembretes", "Notificacoes", "number") },
            
            // Sistema
            { "Sistema.Nome", ("PsiiAnaSantos", "Nome do sistema", "Sistema", "string") },
            { "Sistema.Email", ("psiana@psiianasantos.com.br", "Email principal do sistema", "Sistema", "string") },
            { "Sistema.Telefone", ("(42) 99936-9724", "Telefone de contato", "Sistema", "string") },
            { "Sistema.Endereco", ("Rua Orlando Ferreira Neto, 39 - Jd Itapoã, Londrina - PR, 86043-470", "Endereço da clínica", "Sistema", "string") },
            
            // Consultas
            { "Consultas.DuracaoPadrao", ("50", "Duração padrão das consultas em minutos", "Consultas", "number") },
            { "Consultas.IntervaloMinimo", ("15", "Intervalo mínimo entre consultas em minutos", "Consultas", "number") },
            { "Consultas.ValorPadrao", ("150.00", "Valor padrão da consulta", "Consultas", "number") },
            
            // PsicoPontos
            { "PsicoPontos.PontosParaConsultaGratuita", ("10", "Quantidade de pontos necessários para consulta gratuita", "PsicoPontos", "number") },
            { "PsicoPontos.PontosPorConsulta", ("1", "Pontos ganhos por consulta realizada", "PsicoPontos", "number") },
            
            // Backup
            { "Backup.Automatico.Habilitado", ("false", "Habilitar backup automático", "Backup", "boolean") },
            { "Backup.Automatico.Horario", ("02:00", "Horário do backup automático (HH:mm)", "Backup", "string") },
            { "Backup.Automatico.DiasRetencao", ("30", "Dias de retenção dos backups", "Backup", "number") },
            
            // Segurança
            { "Seguranca.SessaoTimeout", ("30", "Tempo de expiração da sessão em minutos", "Seguranca", "number") },
            { "Seguranca.TentativasLoginMax", ("5", "Número máximo de tentativas de login", "Seguranca", "number") }
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

    /// <summary>
    /// Obtém configurações de notificações
    /// </summary>
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

    /// <summary>
    /// Obtém configurações do sistema
    /// </summary>
    public async Task<SistemaConfig> ObterConfigSistemaAsync()
    {
        return new SistemaConfig
        {
            Nome = await ObterValorStringAsync("Sistema.Nome", "ClinicaPsi"),
            Email = await ObterValorStringAsync("Sistema.Email", "contato@clinicapsi.com"),
            Telefone = await ObterValorStringAsync("Sistema.Telefone", "(00) 00000-0000"),
            Endereco = await ObterValorStringAsync("Sistema.Endereco", "")
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
}

#endregion
