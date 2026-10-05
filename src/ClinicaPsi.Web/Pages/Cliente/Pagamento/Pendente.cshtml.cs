using System.Security.Claims;
using ClinicaPsi.Application.Services.MercadoPago;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClinicaPsi.Web.Pages.Cliente.Pagamento;

[Authorize(Roles = "Cliente")]
public class PendenteModel : PageModel
{
    private readonly MercadoPagoService _mp;

    public PendenteModel(MercadoPagoService mp) => _mp = mp;

    public Consulta? Consulta { get; set; }

    public async Task<IActionResult> OnGetAsync(int consultaId, string? payment_id = null, string? collection_id = null)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return RedirectToPage("/Account/Login");

        var owned = await _mp.GetConsultaDoClienteAsync(consultaId, userId);
        if (owned is null)
            return RedirectToPage("/Cliente/MinhasConsultas");

        Consulta = owned.Value.Consulta;
        var pid = payment_id ?? collection_id;
        if (!string.IsNullOrWhiteSpace(pid) && Consulta.StatusPagamento != StatusPagamento.Pago)
        {
            try { await _mp.SyncPaymentByIdAsync(consultaId, pid); }
            catch { }
            owned = await _mp.GetConsultaDoClienteAsync(consultaId, userId);
            Consulta = owned?.Consulta;
            if (Consulta?.StatusPagamento == StatusPagamento.Pago)
                return RedirectToPage("./Sucesso", new { consultaId });
        }

        return Page();
    }
}
