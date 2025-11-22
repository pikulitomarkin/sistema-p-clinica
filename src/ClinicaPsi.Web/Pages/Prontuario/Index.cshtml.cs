using ClinicaPsi.Application.Services;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace ClinicaPsi.Web.Pages.Prontuario;

[Authorize(Roles = "Admin,Psicologo")]
public class IndexModel : PageModel
{
    private readonly ProntuarioService _prontuarioService;
    private readonly ConsultaService _consultaService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(ProntuarioService prontuarioService, ConsultaService consultaService, ILogger<IndexModel> logger)
    {
        _prontuarioService = prontuarioService;
        _consultaService = consultaService;
        _logger = logger;
    }

    public List<ProntuarioEletronico>? Prontuarios { get; set; }
    public int TotalProntuarios { get; set; }
    public int ProntuariosFinalizados { get; set; }
    public DateTime DataFiltro { get; set; } = DateTime.Today;
    public string? MensagemErro { get; set; }
    public string? MensagemSucesso { get; set; }

    public async Task OnGetAsync(int? pacienteId = null)
    {
        try
        {
            int psicologoId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            
            if (psicologoId == 0)
            {
                MensagemErro = "Não foi possível identificar o psicólogo logado.";
                return;
            }

            // Buscar prontuários do psicólogo
            if (pacienteId.HasValue)
            {
                // Filtrar por paciente específico
                Prontuarios = await _prontuarioService.ObterPorPacienteAsync(pacienteId.Value);
            }
            else
            {
                Prontuarios = await _prontuarioService.ObterPorPsicologoAsync(psicologoId);
            }

            // Calcular estatísticas
            TotalProntuarios = Prontuarios?.Count ?? 0;
            ProntuariosFinalizados = Prontuarios?.Count(p => p.Finalizado) ?? 0;

            _logger.LogInformation($"Carregados {TotalProntuarios} prontuários para psicólogo {psicologoId}");
        }
        catch (Exception ex)
        {
            MensagemErro = "Erro ao carregar prontuários: " + ex.Message;
            _logger.LogError(ex, "Erro ao carregar prontuários");
        }
    }

    public async Task<IActionResult> OnPostExcluirAsync(int id)
    {
        try
        {
            await _prontuarioService.ExcluirProntuarioAsync(id);
            MensagemSucesso = "Prontuário excluído com sucesso.";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            MensagemErro = "Erro ao excluir prontuário: " + ex.Message;
            _logger.LogError(ex, "Erro ao excluir prontuário");
            return Page();
        }
    }
}
