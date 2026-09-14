using System.Globalization;
using System.Text;
using ClinicaPsi.Shared.Models;

namespace ClinicaPsi.Application.Services;

/// <summary>
/// Gera linha de escrituração Carnê-Leão para recibos Receita Saúde (importação manual).
/// Formato oficial: https://www.gov.br/receitafederal/pt-br/assuntos/meu-imposto-de-renda/pagamento/carne-leao/manual/formato-arquivo
/// Não emite o recibo oficial — apenas prepara o CSV para o profissional importar no e-CAC.
/// </summary>
public static class CarneLeaoEscrituracaoHelper
{
    public const string CodigoRendimento = "R01.001.001";
    public const string CodigoOcupacaoPsicologo = "255";
    public const string RecebidoDePf = "PF";
    public const string IndicadorRecibo = "S";

    /// <summary>
    /// Monta uma linha CSV (separador ;) para um recibo de serviço de saúde.
    /// Campos vazios mantidos conforme layout RFB (CNPJ, IRRF, ind. CPF não informado).
    /// </summary>
    public static string MontarLinhaRecibo(
        DateTime dataPagamento,
        decimal valor,
        string descricao,
        string cpfPagador,
        string cpfBeneficiario,
        string cpfProfissional,
        string? registroProfissional)
    {
        var valorFmt = valor.ToString("0.00", CultureInfo.GetCultureInfo("pt-BR"));
        var desc = Truncate(Sanitize(descricao), 255);
        var crp = Truncate(Sanitize(registroProfissional ?? string.Empty), 15);

        // Layout (recibos Receita Saúde):
        // data; R01.001.001; ocupação; valor; descrição; PF; CPF pagador; CPF beneficiário;
        // (vazio); (CNPJ vazio); (ind IRRF vazio); (valor IRRF vazio); S; CPF profissional; registro
        var campos = new[]
        {
            dataPagamento.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            CodigoRendimento,
            CodigoOcupacaoPsicologo,
            valorFmt,
            desc,
            RecebidoDePf,
            SomenteDigitos(cpfPagador),
            SomenteDigitos(cpfBeneficiario),
            string.Empty, // ind. CPF não informado
            string.Empty, // CNPJ
            string.Empty, // indicador IRRF
            string.Empty, // valor IRRF
            IndicadorRecibo,
            SomenteDigitos(cpfProfissional),
            crp
        };

        return string.Join(';', campos);
    }

    public static byte[] GerarArquivoUtf8(IEnumerable<string> linhas)
    {
        var sb = new StringBuilder();
        foreach (var linha in linhas)
        {
            if (string.IsNullOrWhiteSpace(linha)) continue;
            sb.AppendLine(linha.TrimEnd());
        }

        // BOM ajuda Excel/Windows; Carnê-Leão aceita texto separado por ;
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    public static string SomenteDigitos(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return string.Empty;
        var chars = valor.Where(char.IsDigit).ToArray();
        return new string(chars);
    }

    public static string FormatCpf(string? cpf)
    {
        var d = SomenteDigitos(cpf);
        if (d.Length != 11) return cpf ?? string.Empty;
        return $"{d[..3]}.{d[3..6]}.{d[6..9]}-{d[9..]}";
    }

    private static string Sanitize(string value) =>
        value.Replace(';', ',').Replace('\r', ' ').Replace('\n', ' ').Trim();

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}

/// <summary>
/// Dados para geração do recibo PDF interno (não oficial RFB).
/// </summary>
public class ReciboServicoSaudeDados
{
    public required Paciente Paciente { get; init; }
    public required Psicologo Psicologo { get; init; }
    public required SistemaConfig Clinica { get; init; }
    public DateTime DataPagamento { get; init; }
    public DateTime? DataAtendimento { get; init; }
    public decimal Valor { get; init; }
    public string Descricao { get; init; } = "Consulta psicológica";
    public string? CpfProfissional { get; init; }
    public string? CpfPagador { get; init; }
    public string NumeroRecibo { get; init; } = string.Empty;
}
