using System.Security.Claims;
using ClinicaPsi.Application.Services.MercadoPago;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClinicaPsi.Web.Pages.Cliente.Pagamento;

[Authorize(Roles = "Cliente")]
public class SucessoModel : PageModel
{
    private readonly MercadoPagoService _mp;

    public SucessoModel(MercadoPagoService mp) => _mp = mp;

    public Consulta? Consulta { get; set; }
    public string? PaymentId { get; set; }

    public async Task<IActionResult> OnGetAsync(int consultaId, string? payment_id = null, string? collection_id = null)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return RedirectToPage("/Account/Login");

        var owned = await _mp.GetConsultaDoClienteAsync(consultaId, userId);
        if (owned is null)
            return RedirectToPage("/Cliente/MinhasConsultas");

        Consulta = owned.Value.Consulta;
        PaymentId = payment_id ?? collection_id ?? Consulta.MercadoPagoPaymentId;

        // Sincroniza status se voltou do Checkout Pro com payment_id
        if (!string.IsNullOrWhiteSpace(PaymentId) && Consulta.StatusPagamento != StatusPagamento.Pago)
        {
            try { await _mp.SyncPaymentByIdAsync(consultaId, PaymentId); }
            catch { /* webhook pode completar depois */ }
            owned = await _mp.GetConsultaDoClienteAsync(consultaId, userId);
            Consulta = owned?.Consulta;
        }

        return Page();
    }
}
