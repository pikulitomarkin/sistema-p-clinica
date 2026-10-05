using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClinicaPsi.Application.Services.MercadoPago;

public class MercadoPagoService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MercadoPagoOptions _options;
    private readonly CarteiraService _carteira;
    private readonly ILogger<MercadoPagoService> _logger;

    public MercadoPagoService(
        AppDbContext db,
        IHttpClientFactory httpClientFactory,
        IOptions<MercadoPagoOptions> options,
        CarteiraService carteira,
        ILogger<MercadoPagoService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _carteira = carteira;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.AccessToken) &&
        !string.IsNullOrWhiteSpace(_options.PublicKey);

    public string? PublicKey => _options.PublicKey;
    public bool UseSandbox => _options.UseSandbox;

    public string PublicAppUrl =>
        (_options.PublicAppUrl ?? "https://psyall.com.br").TrimEnd('/');

    public static decimal ResolveValorConsulta(Psicologo psicologo)
    {
        if (psicologo.ValorContratoConsulta > 0)
            return psicologo.ValorContratoConsulta;
        if (psicologo.ValorConsulta > 0)
            return psicologo.ValorConsulta;
        return 50m;
    }

    public async Task<(Consulta Consulta, ApplicationUser User)?> GetConsultaDoClienteAsync(
        int consultaId, string userId, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user?.PacienteId is null)
            return null;

        var consulta = await _db.Consultas
            .Include(c => c.Psicologo)
            .Include(c => c.Paciente)
            .FirstOrDefaultAsync(c => c.Id == consultaId && c.PacienteId == user.PacienteId.Value, ct);

        if (consulta is null)
            return null;

        return (consulta, user);
    }

    public async Task<PreferenceResult> CreateOrRefreshPreferenceAsync(Consulta consulta, CancellationToken ct = default)
    {
        EnsureConfigured();

        var baseUrl = PublicAppUrl;
        var valor = consulta.Valor > 0 ? consulta.Valor : 50m;
        var psicologoNome = consulta.Psicologo?.Nome ?? "Psicólogo";
        var pacienteEmail = consulta.Paciente?.Email;
        var pacienteNome = consulta.Paciente?.Nome;

        var payload = new Dictionary<string, object?>
        {
            ["external_reference"] = $"consulta-{consulta.Id}",
            ["notification_url"] = $"{baseUrl}/webhook",
            ["statement_descriptor"] = "PSYALL",
            ["binary_mode"] = false,
            ["items"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["id"] = $"consulta-{consulta.Id}",
                    ["title"] = $"Consulta PsyAll — {psicologoNome}",
                    ["description"] = $"Consulta em {consulta.DataHorario:dd/MM/yyyy HH:mm}",
                    ["quantity"] = 1,
                    ["currency_id"] = "BRL",
                    ["unit_price"] = decimal.Round(valor, 2)
                }
            },
            ["back_urls"] = new Dictionary<string, string>
            {
                ["success"] = $"{baseUrl}/Cliente/Pagamento/Sucesso?consultaId={consulta.Id}",
                ["failure"] = $"{baseUrl}/Cliente/Pagamento/Falha?consultaId={consulta.Id}",
                ["pending"] = $"{baseUrl}/Cliente/Pagamento/Pendente?consultaId={consulta.Id}"
            },
            ["auto_return"] = "approved",
            ["payment_methods"] = new Dictionary<string, object?>
            {
                ["excluded_payment_types"] = Array.Empty<object>(),
                ["installments"] = 1
            },
            ["metadata"] = new Dictionary<string, object?>
            {
                ["consulta_id"] = consulta.Id,
                ["paciente_id"] = consulta.PacienteId
            }
        };

        if (!string.IsNullOrWhiteSpace(pacienteEmail))
        {
            payload["payer"] = new Dictionary<string, object?>
            {
                ["email"] = pacienteEmail,
                ["name"] = pacienteNome
            };
        }

        var json = await PostAsync("/checkout/preferences", payload, ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var preferenceId = root.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Preferência sem id.");
        var initPoint = root.TryGetProperty("init_point", out var ip) ? ip.GetString() : null;
        var sandboxInit = root.TryGetProperty("sandbox_init_point", out var sip) ? sip.GetString() : null;

        consulta.MercadoPagoPreferenceId = preferenceId;
        if (consulta.StatusPagamento == StatusPagamento.Pendente ||
            consulta.StatusPagamento == StatusPagamento.Falhou)
        {
            consulta.StatusPagamento = StatusPagamento.Aguardando;
        }
        consulta.DataAtualizacao = DateTime.Now;
        await _db.SaveChangesAsync(ct);

        return new PreferenceResult(
            preferenceId,
            _options.UseSandbox ? (sandboxInit ?? initPoint) : (initPoint ?? sandboxInit),
            valor);
    }

    public async Task<PaymentProcessResult> ProcessBrickPaymentAsync(
        Consulta consulta,
        JsonElement formData,
        CancellationToken ct = default)
    {
        EnsureConfigured();

        var valor = consulta.Valor > 0 ? consulta.Valor : 50m;
        using var payloadDoc = JsonDocument.Parse(formData.GetRawText());
        var root = payloadDoc.RootElement;

        var body = new Dictionary<string, object?>
        {
            ["transaction_amount"] = decimal.Round(valor, 2),
            ["description"] = $"Consulta PsyAll #{consulta.Id}",
            ["external_reference"] = $"consulta-{consulta.Id}",
            ["notification_url"] = $"{PublicAppUrl}/webhook",
            ["metadata"] = new Dictionary<string, object?>
            {
                ["consulta_id"] = consulta.Id
            }
        };

        CopyIfPresent(root, body, "token");
        CopyIfPresent(root, body, "issuer_id");
        CopyIfPresent(root, body, "payment_method_id");
        CopyIfPresent(root, body, "installments");
        CopyIfPresent(root, body, "payer");
        CopyIfPresent(root, body, "additional_info");
        CopyIfPresent(root, body, "transaction_details");
        CopyIfPresent(root, body, "callback_url");

        if (!body.ContainsKey("installments"))
            body["installments"] = 1;

        var idempotency = $"consulta-{consulta.Id}-{Guid.NewGuid():N}";
        var json = await PostAsync("/v1/payments", body, ct, idempotency);
        using var result = JsonDocument.Parse(json);
        var payment = result.RootElement;

        var paymentId = payment.TryGetProperty("id", out var idEl)
            ? idEl.ToString()
            : null;
        var status = payment.TryGetProperty("status", out var st) ? st.GetString() : null;
        var statusDetail = payment.TryGetProperty("status_detail", out var sd) ? sd.GetString() : null;

        await ApplyPaymentStatusAsync(consulta, paymentId, status, ct);

        return new PaymentProcessResult(paymentId, status, statusDetail, MapStatus(status));
    }

    /// <summary>
    /// Cria pagamento PIX para depósito na carteira do paciente.
    /// external_reference = deposito-{movimentacaoId}
    /// </summary>
    public async Task<PixDepositoResult> CriarDepositoPixAsync(
        int pacienteId,
        Paciente paciente,
        decimal valor,
        CancellationToken ct = default)
    {
        EnsureConfigured();

        if (valor < 5m)
            throw new InvalidOperationException("Valor mínimo de depósito é R$ 5,00.");
        if (valor > 5000m)
            throw new InvalidOperationException("Valor máximo de depósito é R$ 5.000,00.");

        var carteira = await _carteira.ObterOuCriarAsync(pacienteId, ct);

        var mov = new MovimentacaoCarteira
        {
            CarteiraClienteId = carteira.Id,
            Tipo = TipoMovimentacaoCarteira.Credito,
            Valor = decimal.Round(valor, 2),
            SaldoApos = carteira.Saldo,
            Descricao = "Depósito via PIX",
            Status = StatusMovimentacaoCarteira.Pendente,
            DataCriacao = DateTime.Now
        };
        _db.MovimentacoesCarteira.Add(mov);
        await _db.SaveChangesAsync(ct);

        mov.ExternalReference = $"deposito-{mov.Id}";
        await _db.SaveChangesAsync(ct);

        var body = new Dictionary<string, object?>
        {
            ["transaction_amount"] = decimal.Round(valor, 2),
            ["description"] = $"PsyAll — depósito carteira #{mov.Id}",
            ["payment_method_id"] = "pix",
            ["external_reference"] = mov.ExternalReference,
            ["notification_url"] = $"{PublicAppUrl}/webhook",
            ["metadata"] = new Dictionary<string, object?>
            {
                ["tipo"] = "deposito_carteira",
                ["movimentacao_id"] = mov.Id,
                ["paciente_id"] = pacienteId,
                ["carteira_id"] = carteira.Id
            },
            ["payer"] = new Dictionary<string, object?>
            {
                ["email"] = string.IsNullOrWhiteSpace(paciente.Email)
                    ? $"paciente{pacienteId}@psyall.com.br"
                    : paciente.Email,
                ["first_name"] = paciente.Nome
            }
        };

        var idempotency = $"deposito-{mov.Id}-{Guid.NewGuid():N}";
        var json = await PostAsync("/v1/payments", body, ct, idempotency);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var paymentId = root.TryGetProperty("id", out var idEl) ? idEl.ToString() : null;
        var status = root.TryGetProperty("status", out var st) ? st.GetString() : null;

        string? qrCode = null;
        string? qrBase64 = null;
        DateTime? expiraEm = null;

        if (root.TryGetProperty("point_of_interaction", out var poi) &&
            poi.TryGetProperty("transaction_data", out var td))
        {
            qrCode = td.TryGetProperty("qr_code", out var q) ? q.GetString() : null;
            qrBase64 = td.TryGetProperty("qr_code_base64", out var qb) ? qb.GetString() : null;
        }

        if (root.TryGetProperty("date_of_expiration", out var exp) &&
            DateTime.TryParse(exp.GetString(), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var expDt))
        {
            expiraEm = expDt.Kind == DateTimeKind.Utc ? expDt.ToLocalTime() : expDt;
        }

        mov.MercadoPagoPaymentId = paymentId;
        mov.PixCopiaECola = qrCode;
        mov.PixQrCodeBase64 = qrBase64;
        mov.PixExpiraEm = expiraEm ?? DateTime.Now.AddMinutes(30);

        if (string.Equals(status, "approved", StringComparison.OrdinalIgnoreCase))
        {
            await _db.SaveChangesAsync(ct);
            await _carteira.CreditarDepositoAprovadoAsync(mov.Id, paymentId ?? "", valor, ct);
            await _db.Entry(mov).ReloadAsync(ct);
        }
        else
        {
            await _db.SaveChangesAsync(ct);
        }

        return new PixDepositoResult(
            mov.Id,
            paymentId,
            status,
            mov.PixCopiaECola,
            mov.PixQrCodeBase64,
            mov.PixExpiraEm,
            decimal.Round(valor, 2),
            mov.Status == StatusMovimentacaoCarteira.Confirmada);
    }

    public async Task SyncDepositoByPaymentIdAsync(int movimentacaoId, string paymentId, CancellationToken ct = default)
    {
        var json = await GetAsync($"/v1/payments/{paymentId}", ct);
        using var doc = JsonDocument.Parse(json);
        await ApplyDepositoFromPaymentAsync(doc.RootElement, movimentacaoId, paymentId, ct);
    }

    public async Task HandleWebhookAsync(string? topic, string? id, string? dataId, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("Webhook MP ignorado: credenciais não configuradas.");
            return;
        }

        var paymentId = dataId ?? id;
        if (string.IsNullOrWhiteSpace(paymentId))
        {
            _logger.LogWarning("Webhook MP sem id de pagamento (topic={Topic}).", topic);
            return;
        }

        // Aceita payment / merchant_order
        if (!string.IsNullOrWhiteSpace(topic) &&
            !topic.Contains("payment", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(topic, "topic_merchant_order_wh", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Webhook MP ignorado (topic={Topic}).", topic);
            return;
        }

        try
        {
            var json = await GetAsync($"/v1/payments/{paymentId}", ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var status = root.TryGetProperty("status", out var st) ? st.GetString() : null;
            var externalRef = root.TryGetProperty("external_reference", out var er) ? er.GetString() : null;

            // Depósito carteira: deposito-{movId}
            if (!string.IsNullOrWhiteSpace(externalRef) &&
                externalRef.StartsWith("deposito-", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(externalRef["deposito-".Length..], out var movId))
            {
                await ApplyDepositoFromPaymentAsync(root, movId, paymentId, ct);
                _logger.LogInformation(
                    "Webhook MP: depósito {MovId} payment={PaymentId} status={Status}",
                    movId, paymentId, status);
                return;
            }

            // Metadata tipo=deposito_carteira
            if (root.TryGetProperty("metadata", out var metaDep) &&
                metaDep.TryGetProperty("tipo", out var tipoEl) &&
                string.Equals(tipoEl.GetString(), "deposito_carteira", StringComparison.OrdinalIgnoreCase))
            {
                int? mid = null;
                if (metaDep.TryGetProperty("movimentacao_id", out var midEl))
                {
                    if (midEl.ValueKind == JsonValueKind.Number)
                        mid = midEl.GetInt32();
                    else if (int.TryParse(midEl.GetString(), out var midParsed))
                        mid = midParsed;
                }

                if (mid.HasValue)
                {
                    await ApplyDepositoFromPaymentAsync(root, mid.Value, paymentId, ct);
                    _logger.LogInformation(
                        "Webhook MP: depósito (meta) {MovId} payment={PaymentId} status={Status}",
                        mid, paymentId, status);
                    return;
                }
            }

            int? consultaId = null;

            if (!string.IsNullOrWhiteSpace(externalRef) &&
                externalRef.StartsWith("consulta-", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(externalRef["consulta-".Length..], out var parsed))
            {
                consultaId = parsed;
            }
            else if (root.TryGetProperty("metadata", out var meta) &&
                     meta.TryGetProperty("consulta_id", out var cid))
            {
                if (cid.ValueKind == JsonValueKind.Number)
                    consultaId = cid.GetInt32();
                else if (int.TryParse(cid.GetString(), out var fromStr))
                    consultaId = fromStr;
            }

            if (consultaId is null)
            {
                _logger.LogWarning("Webhook MP payment {PaymentId}: referência não identificada (ref={Ref}).",
                    paymentId, externalRef);
                return;
            }

            var consulta = await _db.Consultas.FirstOrDefaultAsync(c => c.Id == consultaId.Value, ct);
            if (consulta is null)
            {
                _logger.LogWarning("Webhook MP: consulta {ConsultaId} não encontrada.", consultaId);
                return;
            }

            await ApplyPaymentStatusAsync(consulta, paymentId, status, ct);
            _logger.LogInformation(
                "Webhook MP: consulta {ConsultaId} payment={PaymentId} status={Status}",
                consultaId, paymentId, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro processando webhook MP payment={PaymentId}", paymentId);
            throw;
        }
    }

    private async Task ApplyDepositoFromPaymentAsync(
        JsonElement root, int movimentacaoId, string paymentId, CancellationToken ct)
    {
        var status = root.TryGetProperty("status", out var st) ? st.GetString() : null;
        var mapped = MapStatus(status);
        var amount = 0m;
        if (root.TryGetProperty("transaction_amount", out var amt))
        {
            if (amt.ValueKind == JsonValueKind.Number)
                amount = amt.GetDecimal();
            else
                decimal.TryParse(amt.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out amount);
        }

        if (mapped == StatusPagamento.Pago)
        {
            await _carteira.CreditarDepositoAprovadoAsync(movimentacaoId, paymentId, amount, ct);
        }
        else if (mapped == StatusPagamento.Falhou || mapped == StatusPagamento.Cancelado)
        {
            await _carteira.MarcarDepositoFalhouAsync(movimentacaoId, paymentId, ct);
        }
        else
        {
            var mov = await _db.MovimentacoesCarteira.FirstOrDefaultAsync(m => m.Id == movimentacaoId, ct);
            if (mov is not null && mov.Status == StatusMovimentacaoCarteira.Pendente)
            {
                mov.MercadoPagoPaymentId = paymentId;
                await _db.SaveChangesAsync(ct);
            }
        }
    }

    public async Task SyncPaymentByIdAsync(int consultaId, string paymentId, CancellationToken ct = default)
    {
        var consulta = await _db.Consultas.FirstOrDefaultAsync(c => c.Id == consultaId, ct)
            ?? throw new InvalidOperationException("Consulta não encontrada.");
        var json = await GetAsync($"/v1/payments/{paymentId}", ct);
        using var doc = JsonDocument.Parse(json);
        var status = doc.RootElement.TryGetProperty("status", out var st) ? st.GetString() : null;
        await ApplyPaymentStatusAsync(consulta, paymentId, status, ct);
    }

    private async Task ApplyPaymentStatusAsync(
        Consulta consulta, string? paymentId, string? mpStatus, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(paymentId))
            consulta.MercadoPagoPaymentId = paymentId;

        var mapped = MapStatus(mpStatus);
        consulta.StatusPagamento = mapped;
        consulta.DataAtualizacao = DateTime.Now;

        if (mapped == StatusPagamento.Pago)
        {
            consulta.PaidAt ??= DateTime.Now;
            if (consulta.Status == StatusConsulta.Agendada)
                consulta.Status = StatusConsulta.Confirmada;
            consulta.ConfirmacaoRecebida = true;
        }

        await _db.SaveChangesAsync(ct);
    }

    public static StatusPagamento MapStatus(string? mpStatus) =>
        (mpStatus ?? string.Empty).ToLowerInvariant() switch
        {
            "approved" => StatusPagamento.Pago,
            "authorized" => StatusPagamento.Pago,
            "pending" or "in_process" or "in_mediation" => StatusPagamento.Aguardando,
            "rejected" or "cancelled" => StatusPagamento.Falhou,
            "refunded" or "charged_back" => StatusPagamento.Reembolsado,
            _ => StatusPagamento.Aguardando
        };

    private void EnsureConfigured()
    {
        if (!IsConfigured)
            throw new InvalidOperationException(
                "Mercado Pago não configurado. Defina MercadoPago__AccessToken e MercadoPago__PublicKey.");
    }

    private HttpClient CreateClient(string? idempotencyKey = null)
    {
        var client = _httpClientFactory.CreateClient("MercadoPago");
        client.BaseAddress ??= new Uri("https://api.mercadopago.com/");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.AccessToken);
        client.DefaultRequestHeaders.Remove("X-Idempotency-Key");
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Idempotency-Key", idempotencyKey);
        return client;
    }

    private async Task<string> PostAsync(
        string path, object body, CancellationToken ct, string? idempotencyKey = null)
    {
        var client = CreateClient(idempotencyKey);
        var content = new StringContent(
            JsonSerializer.Serialize(body, JsonOpts),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync(path, content, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("MP POST {Path} falhou {Status}: {Body}", path, (int)response.StatusCode, text);
            throw new InvalidOperationException($"Mercado Pago retornou {(int)response.StatusCode}: {Truncate(text)}");
        }
        return text;
    }

    private async Task<string> GetAsync(string path, CancellationToken ct)
    {
        var client = CreateClient();
        var response = await client.GetAsync(path, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("MP GET {Path} falhou {Status}: {Body}", path, (int)response.StatusCode, text);
            throw new InvalidOperationException($"Mercado Pago retornou {(int)response.StatusCode}: {Truncate(text)}");
        }
        return text;
    }

    private static void CopyIfPresent(JsonElement root, Dictionary<string, object?> body, string name)
    {
        if (root.TryGetProperty(name, out var el) && el.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null)
            body[name] = JsonSerializer.Deserialize<object>(el.GetRawText());
    }

    private static string Truncate(string s) =>
        s.Length <= 400 ? s : s[..400] + "…";
}

public record PreferenceResult(string PreferenceId, string? CheckoutUrl, decimal Amount);

public record PaymentProcessResult(
    string? PaymentId,
    string? Status,
    string? StatusDetail,
    StatusPagamento MappedStatus);

public record PixDepositoResult(
    int MovimentacaoId,
    string? PaymentId,
    string? Status,
    string? PixCopiaECola,
    string? PixQrCodeBase64,
    DateTime? ExpiraEm,
    decimal Valor,
    bool JaAprovado);
