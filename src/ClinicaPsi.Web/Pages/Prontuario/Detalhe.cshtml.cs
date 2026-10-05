using ClinicaPsi.Application.Services;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace ClinicaPsi.Web.Pages.Prontuario;

[Authorize(Roles = "Admin,Psicologo")]
public class DetalheModel : PageModel
{
    private readonly ProntuarioService _prontuarioService;
    private readonly PacienteService _pacienteService;
    private readonly PsicologoService _psicologoService;
    private readonly PdfService _pdfService;
    private readonly ConfiguracaoService _configuracaoService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<DetalheModel> _logger;

    public DetalheModel(
        ProntuarioService prontuarioService,
        PacienteService pacienteService,
        PsicologoService psicologoService,
        PdfService pdfService,
        ConfiguracaoService configuracaoService,
        UserManager<ApplicationUser> userManager,
        ILogger<DetalheModel> logger)
    {
        _prontuarioService = prontuarioService;
        _pacienteService = pacienteService;
        _psicologoService = psicologoService;
        _pdfService = pdfService;
        _configuracaoService = configuracaoService;
        _userManager = userManager;
        _logger = logger;
    }

    public ProntuarioEletronico? Prontuario { get; set; }
    public Paciente? Paciente { get; set; }
    public Shared.Models.Psicologo? Psicologo { get; set; }
    public string? MensagemErro { get; set; }
    public string? MensagemSucesso { get; set; }

    [BindProperty]
    public string? NovaEvolucao { get; set; }

    private async Task<bool> PodeAcessarAsync(ProntuarioEletronico prontuario)
    {
        if (User.IsInRole("Admin"))
            return true;

        var user = await _userManager.GetUserAsync(User);
        var todos = await _psicologoService.GetAllAsync();
        var psicologo = todos.FirstOrDefault(p => p.UserId == user?.Id)
            ?? (user?.PsicologoId != null ? todos.FirstOrDefault(p => p.Id == user.PsicologoId) : null);
        return psicologo != null && prontuario.PsicologoId == psicologo.Id;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        try
        {
            if (!await _configuracaoService.ObterValorBoolAsync("Prontuario.Habilitado", true))
            {
                MensagemErro = "O prontuário eletrônico está desabilitado nas configurações do sistema.";
                return Page();
            }

            Prontuario = await _prontuarioService.ObterPorIdAsync(id);

            if (Prontuario == null)
            {
                MensagemErro = "Prontuário não encontrado.";
                return Page();
            }

            if (!await PodeAcessarAsync(Prontuario))
                return Forbid();

            Paciente = await _pacienteService.GetByIdAsync(Prontuario.PacienteId);
            Psicologo = await _psicologoService.GetByIdAsync(Prontuario.PsicologoId);
            return Page();
        }
        catch (Exception ex)
        {
            MensagemErro = "Erro ao carregar prontuário: " + ex.Message;
            _logger.LogError(ex, "Erro ao carregar prontuário");
            return Page();
        }
    }

    public async Task<IActionResult> OnPostFinalizarAsync(int id)
    {
        try
        {
            var prontuario = await _prontuarioService.ObterPorIdAsync(id);
            if (prontuario == null)
                return NotFound();
            if (!await PodeAcessarAsync(prontuario))
                return Forbid();

            prontuario.Finalizado = true;
            prontuario.DataAtualizacao = DateTime.Now;
            await _prontuarioService.AtualizarProntuarioAsync(prontuario);

            TempData["SuccessMessage"] = "Prontuário finalizado com sucesso!";
            return RedirectToPage("Index");
        }
        catch (Exception ex)
        {
            MensagemErro = "Erro ao finalizar prontuário: " + ex.Message;
            _logger.LogError(ex, "Erro ao finalizar prontuário");
            return Page();
        }
    }

    public async Task<IActionResult> OnPostAdicionarEvolucaoAsync(int id)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(NovaEvolucao))
            {
                MensagemErro = "A evolução não pode estar vazia.";
                await OnGetAsync(id);
                return Page();
            }

            var prontuario = await _prontuarioService.ObterPorIdAsync(id);
            if (prontuario == null)
                return NotFound();
            if (!await PodeAcessarAsync(prontuario))
                return Forbid();
            if (prontuario.Finalizado)
            {
                MensagemErro = "Prontuário finalizado não pode receber novas evoluções.";
                await OnGetAsync(id);
                return Page();
            }

            var dataHora = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            var evolucaoComData = $"[{dataHora}]\n{NovaEvolucao}";

            if (string.IsNullOrEmpty(prontuario.Evolucao))
                prontuario.Evolucao = evolucaoComData;
            else
                prontuario.Evolucao += "\n\n---\n\n" + evolucaoComData;

            prontuario.DataAtualizacao = DateTime.Now;
            await _prontuarioService.AtualizarProntuarioAsync(prontuario);

            TempData["SuccessMessage"] = "Evolução adicionada com sucesso!";
            return RedirectToPage(new { id });
        }
        catch (Exception ex)
        {
            MensagemErro = "Erro ao adicionar evolução: " + ex.Message;
            _logger.LogError(ex, "Erro ao adicionar evolução");
            return Page();
        }
    }

    public async Task<IActionResult> OnGetDownloadPdfAsync(int id)
    {
        try
        {
            var prontuario = await _prontuarioService.ObterPorIdAsync(id);
            if (prontuario == null)
                return NotFound();
            if (!await PodeAcessarAsync(prontuario))
                return Forbid();

            var pdfBytes = await _pdfService.GerarProntuarioPdfAsync(id);
            var paciente = await _pacienteService.GetByIdAsync(prontuario.PacienteId);
            var fileName = $"Prontuario_{id}_{paciente?.Nome?.Replace(" ", "_") ?? "Paciente"}_{DateTime.Now:yyyyMMdd}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar PDF do prontuário {Id}", id);
            TempData["ErrorMessage"] = "Erro ao gerar PDF: " + ex.Message;
            return RedirectToPage(new { id });
        }
    }
}
