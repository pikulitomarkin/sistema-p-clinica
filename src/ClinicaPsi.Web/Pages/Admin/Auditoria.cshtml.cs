using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ClinicaPsi.Shared.Models;
using ClinicaPsi.Application.Services;

namespace ClinicaPsi.Web.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class AuditoriaModel : PageModel
    {
        private readonly AuditoriaService _auditoriaService;

        public AuditoriaModel(AuditoriaService auditoriaService)
        {
            _auditoriaService = auditoriaService;
        }

        [BindProperty(SupportsGet = true)]
        public int PaginaAtual { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public string? FiltroAdmin { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? FiltroUsuario { get; set; }

        [BindProperty(SupportsGet = true)]
        public TipoAcaoAuditoria? FiltroAcao { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? DataInicio { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? DataFim { get; set; }

        public List<AuditoriaUsuario> Registros { get; set; } = new();
        public int TotalRegistros { get; set; }
        public int TotalPaginas { get; set; }
        public Dictionary<TipoAcaoAuditoria, int> Estatisticas { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            const int itensPorPagina = 20;

            // Obter registros com paginação
            var (registros, total) = await _auditoriaService.ObterAuditoriasAsync(
                pagina: PaginaAtual,
                itensPorPagina: itensPorPagina,
                filtroAdmin: FiltroAdmin,
                filtroUsuario: FiltroUsuario,
                filtroAcao: FiltroAcao,
                dataInicio: DataInicio,
                dataFim: DataFim
            );

            Registros = registros;
            TotalRegistros = total;
            TotalPaginas = (int)Math.Ceiling(total / (double)itensPorPagina);

            // Obter estatísticas (últimos 30 dias)
            var dataEstatisticas = DateTime.Now.AddDays(-30);
            Estatisticas = await _auditoriaService.ObterEstatisticasAsync(dataEstatisticas);

            return Page();
        }
    }
}
