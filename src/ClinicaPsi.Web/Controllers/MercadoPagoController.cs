using System.Security.Claims;
using System.Text.Json;
using ClinicaPsi.Application.Services.MercadoPago;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicaPsi.Web.Controllers;

[ApiController]
[Route("api/mercadopago")]
public class MercadoPagoController : ControllerBase
{
    private readonly MercadoPagoService _mp;
    private readonly ILogger<MercadoPagoController> _logger;

    public MercadoPagoController(
        MercadoPagoService mp,
        ILogger<MercadoPagoController> logger)
    {
        _mp = mp;
        _logger = logger;
    }

    /// <summary>Cria/atualiza preference e retorna dados para o Payment Brick (cliente dono da consulta).</summary>
    [Authorize(Roles = "Cliente")]
    [HttpPost("preference/{consultaId:int}")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> CreatePreference(int consultaId, CancellationToken ct)
    {
        if (!_mp.IsConfigured)
            return StatusCode(503, new { error = "Pagamentos temporariamente indisponíveis." });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var owned = await _mp.GetConsultaDoClienteAsync(consultaId, userId, ct);
        if (owned is null)
            return NotFound(new { error = "Consulta não encontrada." });

        var (consulta, _) = owned.Value;
        if (consulta.Status == StatusConsulta.Cancelada)
            return BadRequest(new { error = "Consulta cancelada." });

        if (consulta.StatusPagamento == StatusPagamento.Pago)
            return Ok(new { alreadyPaid = true, consultaId = consulta.Id });

        try
        {
            var pref = await _mp.CreateOrRefreshPreferenceAsync(consulta, ct);
            return Ok(new
            {
                preferenceId = pref.PreferenceId,
                publicKey = _mp.PublicKey,
                amount = pref.Amount,
                checkoutUrl = pref.CheckoutUrl,
                useSandbox = _mp.UseSandbox,
                consultaId = consulta.Id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro criando preference consulta {ConsultaId}", consultaId);
            return BadRequest(new { error = "Não foi possível iniciar o pagamento. Tente novamente." });
        }
    }

    /// <summary>Processa pagamento enviado pelo Payment Brick (PIX / cartão).</summary>
    [Authorize(Roles = "Cliente")]
    [HttpPost("process-payment/{consultaId:int}")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ProcessPayment(int consultaId, CancellationToken ct)
    {
        if (!_mp.IsConfigured)
            return StatusCode(503, new { error = "Pagamentos temporariamente indisponíveis." });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var owned = await _mp.GetConsultaDoClienteAsync(consultaId, userId, ct);
        if (owned is null)
            return NotFound(new { error = "Consulta não encontrada." });

        var (consulta, _) = owned.Value;
        if (consulta.StatusPagamento == StatusPagamento.Pago)
            return Ok(new { status = "approved", alreadyPaid = true });

        try
        {
            using var doc = await JsonDocument.ParseAsync(Request.Body, cancellationToken: ct);
            var result = await _mp.ProcessBrickPaymentAsync(consulta, doc.RootElement, ct);

            var redirect = result.MappedStatus switch
            {
                StatusPagamento.Pago => $"/Cliente/Pagamento/Sucesso?consultaId={consulta.Id}",
                StatusPagamento.Falhou => $"/Cliente/Pagamento/Falha?consultaId={consulta.Id}",
                _ => $"/Cliente/Pagamento/Pendente?consultaId={consulta.Id}"
            };

            return Ok(new
            {
                id = result.PaymentId,
                status = result.Status,
                status_detail = result.StatusDetail,
                mappedStatus = result.MappedStatus.ToString(),
                redirectUrl = redirect
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro processando pagamento consulta {ConsultaId}", consultaId);
            return BadRequest(new { error = "Falha ao processar pagamento.", detail = ex.Message });
        }
    }

}
