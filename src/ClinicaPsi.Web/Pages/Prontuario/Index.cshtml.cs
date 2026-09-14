using ClinicaPsi.Application.Services;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace ClinicaPsi.Web.Pages.Prontuario;

[Authorize(Roles = "Admin,Psicologo")]
public class IndexModel : PageModel
{
    private readonly ProntuarioService _prontuarioService;
    private readonly PsicologoService _psicologoService;
    private readonly ConfiguracaoService _configuracaoService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        ProntuarioService prontuarioService,
        PsicologoService psicologoService,
        ConfiguracaoService configuracaoService,
        UserManager<ApplicationUser> userManager,
        ILogger<IndexModel> logger)
    {
        _prontuarioService = prontuarioService;
        _psicologoService = psicologoService;
        _configuracaoService = configuracaoService;
        _userManager = userManager;
        _logger = logger;
    }

    public List<ProntuarioEletronico>? Prontuarios { get; set; }
    public int TotalProntuarios { get; set; }
    public int ProntuariosFinalizados { get; set; }
    public DateTime DataFiltro { get; set; } = DateTime.Today;
    public string? MensagemErro { get; set; }
    public string? MensagemSucesso { get; set; }
    public bool IsAdmin { get; set; }

    public async Task OnGetAsync(int? pacienteId = null)
    {
        try
        {
            if (!await _configuracaoService.ObterValorBoolAsync("Prontuario.Habilitado", true))
            {
                MensagemErro = "O prontuário eletrônico está desabilitado nas configurações do sistema.";
                return;
            }

            IsAdmin = User.IsInRole("Admin");
            var user = await _userManager.GetUserAsync(User);
            string? userId = user?.Id ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            Shared.Models.Psicologo? psicologo = null;
            if (!string.IsNullOrEmpty(userId))
            {
                var todosPsicologos = await _psicologoService.GetAllAsync();
                psicologo = todosPsicologos.FirstOrDefault(p => p.UserId == userId)
                    ?? (user?.PsicologoId != null
                        ? todosPsicologos.FirstOrDefault(p => p.Id == user.PsicologoId)
                        : null);
            }

            if (IsAdmin && psicologo == null)
            {
                if (pacienteId.HasValue)
                    Prontuarios = await _prontuarioService.ObterPorPacienteAsync(pacienteId.Value);
                else
                    Prontuarios = await _prontuarioService.BuscarProntuariosAsync(string.Empty);
            }
            else if (psicologo == null)
            {
                MensagemErro = "Psicólogo não encontrado. Verifique se seu cadastro está completo.";
                return;
            }
            else if (pacienteId.HasValue)
            {
                Prontuarios = await _prontuarioService.ObterPorPacienteAsync(pacienteId.Value);
                if (!IsAdmin)
                    Prontuarios = Prontuarios?.Where(p => p.PsicologoId == psicologo.Id).ToList();
            }
            else
            {
                Prontuarios = await _prontuarioService.ObterPorPsicologoAsync(psicologo.Id);
            }

            TotalProntuarios = Prontuarios?.Count ?? 0;
            ProntuariosFinalizados = Prontuarios?.Count(p => p.Finalizado) ?? 0;

            _logger.LogInformation("Carregados {Count} prontuários", TotalProntuarios);
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
            var prontuario = await _prontuarioService.ObterPorIdAsync(id);
            if (prontuario == null)
                return NotFound();

            if (!User.IsInRole("Admin"))
            {
                var user = await _userManager.GetUserAsync(User);
                var todos = await _psicologoService.GetAllAsync();
                var psicologo = todos.FirstOrDefault(p => p.UserId == user?.Id)
                    ?? (user?.PsicologoId != null ? todos.FirstOrDefault(p => p.Id == user.PsicologoId) : null);
                if (psicologo == null || prontuario.PsicologoId != psicologo.Id)
                    return Forbid();
            }

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
