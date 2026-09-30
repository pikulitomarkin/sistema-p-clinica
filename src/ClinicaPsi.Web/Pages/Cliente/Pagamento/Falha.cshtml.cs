using System.Security.Claims;
using ClinicaPsi.Application.Services.MercadoPago;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClinicaPsi.Web.Pages.Cliente.Pagamento;

[Authorize(Roles = "Cliente")]
public class FalhaModel : PageModel
{
    private readonly MercadoPagoService _mp;

    public FalhaModel(MercadoPagoService mp) => _mp = mp;

    public Consulta? Consulta { get; set; }

    public async Task<IActionResult> OnGetAsync(int consultaId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return RedirectToPage("/Account/Login");

        var owned = await _mp.GetConsultaDoClienteAsync(consultaId, userId);
        if (owned is null)
            return RedirectToPage("/Cliente/MinhasConsultas");

        Consulta = owned.Value.Consulta;
        return Page();
    }
}
