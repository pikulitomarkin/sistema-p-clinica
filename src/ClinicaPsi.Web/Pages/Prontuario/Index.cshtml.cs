using ClinicaPsi.Application.Services;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

[Authorize(Roles = "Admin,Psicologo")]
public class IndexModel : PageModel
{
    private readonly ProntuarioService _prontuarioService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(ProntuarioService prontuarioService, ILogger<IndexModel> logger)
    {
        _prontuarioService = prontuarioService;
        _logger = logger;
    }

    public List<ProntuarioEletronico>? Prontuarios { get; set; }

    public async Task OnGetAsync()
    {
        // Exemplo: buscar todos os prontuários do psicólogo logado
        // Substitua pelo id correto do psicólogo
        int psicologoId = 1;
        Prontuarios = await _prontuarioService.ObterPorPsicologoAsync(psicologoId);
    }
}
