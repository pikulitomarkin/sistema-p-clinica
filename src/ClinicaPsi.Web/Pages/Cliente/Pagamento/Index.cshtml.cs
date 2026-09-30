using System.Security.Claims;
using ClinicaPsi.Application.Services;
using ClinicaPsi.Application.Services.MercadoPago;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClinicaPsi.Web.Pages.Cliente.Pagamento;

[Authorize(Roles = "Cliente")]
public class IndexModel : PageModel
{
    private readonly MercadoPagoService _mp;
    private readonly CarteiraService _carteira;

    public IndexModel(MercadoPagoService mp, CarteiraService carteira)
    {
        _mp = mp;
        _carteira = carteira;
    }

    public Consulta? Consulta { get; set; }
    public string? PublicKey { get; set; }
    public string? PreferenceId { get; set; }
    public string? CheckoutUrl { get; set; }
    public decimal Amount { get; set; }
    public bool JaPago { get; set; }
    public bool MpDisponivel { get; set; }
    public bool UseSandbox { get; set; }
    public string? Erro { get; set; }
    public decimal SaldoCarteira { get; set; }
    public bool PodePagarComCarteira { get; set; }

    public async Task<IActionResult> OnGetAsync(int consultaId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return RedirectToPage("/Account/Login");

        var owned = await _mp.GetConsultaDoClienteAsync(consultaId, userId);
        if (owned is null)
        {
            TempData["Error"] = "Consulta não encontrada ou você não tem permissão.";
            return RedirectToPage("/Cliente/MinhasConsultas");
        }

        Consulta = owned.Value.Consulta;

        if (Consulta.Status == StatusConsulta.Cancelada)
        {
            TempData["Error"] = "Esta consulta foi cancelada.";
            return RedirectToPage("/Cliente/MinhasConsultas");
        }

        if (Consulta.StatusPagamento == StatusPagamento.Pago)
        {
            JaPago = true;
            Amount = Consulta.Valor;
            return Page();
        }

        Amount = Consulta.Valor > 0 ? Consulta.Valor : 50m;
        SaldoCarteira = await _carteira.ObterSaldoAsync(Consulta.PacienteId);
        PodePagarComCarteira = SaldoCarteira >= Amount;

        MpDisponivel = _mp.IsConfigured;
        UseSandbox = _mp.UseSandbox;
        if (!MpDisponivel)
        {
            Erro = "Pagamentos ainda não estão disponíveis. Tente novamente em instantes.";
            return Page();
        }

        try
        {
            var pref = await _mp.CreateOrRefreshPreferenceAsync(Consulta);
            PreferenceId = pref.PreferenceId;
            CheckoutUrl = pref.CheckoutUrl;
            Amount = pref.Amount;
            PublicKey = _mp.PublicKey;
            PodePagarComCarteira = SaldoCarteira >= Amount;
        }
        catch (Exception ex)
        {
            Erro = "Não foi possível iniciar o checkout. " + ex.Message;
            Amount = Consulta.Valor > 0 ? Consulta.Valor : 50m;
            PublicKey = _mp.PublicKey;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostPagarComCarteiraAsync(int consultaId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return RedirectToPage("/Account/Login");

        var owned = await _mp.GetConsultaDoClienteAsync(consultaId, userId);
        if (owned is null)
        {
            TempData["Error"] = "Consulta não encontrada.";
            return RedirectToPage("/Cliente/MinhasConsultas");
        }

        var consulta = owned.Value.Consulta;
        if (consulta.StatusPagamento == StatusPagamento.Pago)
            return RedirectToPage("/Cliente/Pagamento/Sucesso", new { consultaId });

        var ok = await _carteira.TentarDebitarConsultaAsync(consulta);
        if (ok)
        {
            TempData["Success"] = "Consulta paga com saldo da carteira.";
            return RedirectToPage("/Cliente/Pagamento/Sucesso", new { consultaId });
        }

        TempData["Error"] = "Saldo insuficiente. Deposite na carteira ou pague com PIX/cartão.";
        return RedirectToPage(new { consultaId });
    }
}
