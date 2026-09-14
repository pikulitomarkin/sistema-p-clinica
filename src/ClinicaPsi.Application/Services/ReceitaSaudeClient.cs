namespace ClinicaPsi.Application.Services;

/// <summary>
/// Placeholder para futura integração oficial com Receita Saúde / Carnê-Leão.
/// Hoje a RFB não documenta API REST pública de emissão; canais oficiais:
/// App Receita Federal, Carnê-Leão Web e importação de escrituração (CSV).
/// </summary>
public interface IReceitaSaudeClient
{
    /// <summary>Indica se um cliente de API remoto está configurado e habilitado.</summary>
    bool EstaHabilitado { get; }

    /// <summary>
    /// Reservado: emitir recibo via API oficial quando existir.
    /// Implementação atual sempre falha de forma explícita.
    /// </summary>
    Task<ReceitaSaudeEmissaoResultado> EmitirReciboAsync(
        ReceitaSaudeEmissaoRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ReceitaSaudeEmissaoRequest
{
    public DateTime DataPagamento { get; init; }
    public decimal Valor { get; init; }
    public string Descricao { get; init; } = string.Empty;
    public string CpfPagador { get; init; } = string.Empty;
    public string CpfBeneficiario { get; init; } = string.Empty;
    public string CpfProfissional { get; init; } = string.Empty;
    public string? RegistroProfissional { get; init; }
}

public sealed class ReceitaSaudeEmissaoResultado
{
    public bool Sucesso { get; init; }
    public string? NumeroReciboOficial { get; init; }
    public string? Mensagem { get; init; }
}

/// <summary>
/// Adapter desabilitado: sem endpoint RFB usável. Credenciais (se um dia existirem)
/// devem vir só de variáveis de ambiente, nunca do repositório.
/// </summary>
public sealed class NullReceitaSaudeClient : IReceitaSaudeClient
{
    public const string EnvApiBaseUrl = "RECEITA_SAUDE_API_BASE_URL";
    public const string EnvClientId = "RECEITA_SAUDE_CLIENT_ID";
    public const string EnvClientSecret = "RECEITA_SAUDE_CLIENT_SECRET";

    public bool EstaHabilitado => false;

    public Task<ReceitaSaudeEmissaoResultado> EmitirReciboAsync(
        ReceitaSaudeEmissaoRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ReceitaSaudeEmissaoResultado
        {
            Sucesso = false,
            Mensagem =
                "API oficial de emissão Receita Saúde não está disponível para integração. " +
                "Use o App Receita Federal / Carnê-Leão Web, ou exporte o CSV de escrituração pela aba Documentos. " +
                $"Placeholders de config (env): {EnvApiBaseUrl}, {EnvClientId}, {EnvClientSecret}."
        });
    }
}
