using ClinicaPsi.Application.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClinicaPsi.Web.Pages.Privacidade;

public class IndexModel : PageModel
{
    private readonly ConfiguracaoService _config;

    public IndexModel(ConfiguracaoService config)
    {
        _config = config;
    }

    public SistemaConfig Clinica { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Clinica = await _config.ObterConfigSistemaAsync();
    }
}
