using System.Security.Claims;
using ClinicaPsi.Application.Services.MercadoPago;
using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ClinicaPsi.Web.Pages.Cliente.Pagamento;

[Authorize(Roles = "Cliente")]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly MercadoPagoService _mp;

    public IndexModel(AppDbContext db, MercadoPagoService mp)
    {
        _db = db;
        _mp = mp;
    }

    public Consulta? Consulta { get; set; }
    public string? PublicKey { get; set; }
    public string? PreferenceId { get; set; }
    public string? CheckoutUrl { get; set; }
    public decimal Amount { get; set; }
    public bool JaPago { get; set; }
    public bool MpDisponivel { get; set; }
    public string? Erro { get; set; }

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

        MpDisponivel = _mp.IsConfigured;
        if (!MpDisponivel)
        {
            Erro = "Pagamentos ainda não estão disponíveis. Tente novamente em instantes.";
            Amount = Consulta.Valor > 0 ? Consulta.Valor : 50m;
            return Page();
        }

        try
        {
            var pref = await _mp.CreateOrRefreshPreferenceAsync(Consulta);
            PreferenceId = pref.PreferenceId;
            CheckoutUrl = pref.CheckoutUrl;
            Amount = pref.Amount;
            PublicKey = _mp.PublicKey;
        }
        catch (Exception ex)
        {
            Erro = "Não foi possível iniciar o checkout. " + ex.Message;
            Amount = Consulta.Valor > 0 ? Consulta.Valor : 50m;
            PublicKey = _mp.PublicKey;
        }

        return Page();
    }
}
