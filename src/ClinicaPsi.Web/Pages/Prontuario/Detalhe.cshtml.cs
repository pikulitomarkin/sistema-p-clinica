using ClinicaPsi.Application.Services;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;

[Authorize(Roles = "Admin,Psicologo")]
public class DetalheModel : PageModel
{
    private readonly ProntuarioService _prontuarioService;

    public DetalheModel(ProntuarioService prontuarioService)
    {
        _prontuarioService = prontuarioService;
    }

    public ProntuarioEletronico? Prontuario { get; set; }

    public async Task OnGetAsync(int id)
    {
        Prontuario = await _prontuarioService.ObterPorIdAsync(id);
    }
}
