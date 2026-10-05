using ClinicaPsi.Application.Services;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace ClinicaPsi.Web.Pages.Prontuario;

[Authorize(Roles = "Admin,Psicologo")]
public class CadastroModel : PageModel
{
    private readonly ProntuarioService _prontuarioService;
    private readonly PacienteService _pacienteService;
    private readonly PsicologoService _psicologoService;
    private readonly ConfiguracaoService _configuracaoService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<CadastroModel> _logger;

    public CadastroModel(
        ProntuarioService prontuarioService,
        PacienteService pacienteService,
        PsicologoService psicologoService,
        ConfiguracaoService configuracaoService,
        UserManager<ApplicationUser> userManager,
        ILogger<CadastroModel> logger)
    {
        _prontuarioService = prontuarioService;
        _pacienteService = pacienteService;
        _psicologoService = psicologoService;
        _configuracaoService = configuracaoService;
        _userManager = userManager;
        _logger = logger;
    }

    [BindProperty]
    public ProntuarioEletronico Prontuario { get; set; } = new();

    public List<Paciente> Pacientes { get; set; } = new();
    public List<Shared.Models.Psicologo> Psicologos { get; set; } = new();

    public bool Edicao => Prontuario.Id > 0;
    public string? MensagemErro { get; set; }
    public string? MensagemSucesso { get; set; }

    [BindProperty]
    public List<IFormFile>? ArquivoAnexo { get; set; }

    [BindProperty]
    public string? NovaEvolucao { get; set; }

    private async Task<Shared.Models.Psicologo?> ObterPsicologoLogadoAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        var todos = await _psicologoService.GetAllAsync();
        return todos.FirstOrDefault(p => p.UserId == user?.Id)
            ?? (user?.PsicologoId != null ? todos.FirstOrDefault(p => p.Id == user.PsicologoId) : null);
    }

    private async Task<bool> PodeAcessarAsync(ProntuarioEletronico prontuario)
    {
        if (User.IsInRole("Admin"))
            return true;
        var psicologo = await ObterPsicologoLogadoAsync();
        return psicologo != null && prontuario.PsicologoId == psicologo.Id;
    }

    public async Task<IActionResult> OnGetAsync(int? id, int? pacienteId = null, int? consultaId = null)
    {
        try
        {
            if (!await _configuracaoService.ObterValorBoolAsync("Prontuario.Habilitado", true))
            {
                MensagemErro = "O prontuário eletrônico está desabilitado nas configurações do sistema.";
                return Page();
            }

            Pacientes = await _pacienteService.GetAllAsync();
            Psicologos = await _psicologoService.GetAllAsync();

            if (id.HasValue)
            {
                var prontuario = await _prontuarioService.ObterPorIdAsync(id.Value);
                if (prontuario == null)
                {
                    MensagemErro = "Prontuário não encontrado.";
                    return Page();
                }
                if (!await PodeAcessarAsync(prontuario))
                    return Forbid();
                if (prontuario.Finalizado)
                {
                    TempData["ErrorMessage"] = "Prontuário finalizado não pode ser editado.";
                    return RedirectToPage("Detalhe", new { id = prontuario.Id });
                }
                Prontuario = prontuario;
            }
            else
            {
                var psicologoLogado = await ObterPsicologoLogadoAsync();
                if (psicologoLogado != null)
                    Prontuario.PsicologoId = psicologoLogado.Id;

                if (pacienteId.HasValue)
                    Prontuario.PacienteId = pacienteId.Value;

                if (consultaId.HasValue)
                {
                    Prontuario.ConsultaId = consultaId.Value;
                    var existente = await _prontuarioService.ObterPorConsultaAsync(consultaId.Value);
                    if (existente != null)
                        return RedirectToPage("Detalhe", new { id = existente.Id });
                }

                Prontuario.DataSessao = DateTime.Today;
            }

            return Page();
        }
        catch (Exception ex)
        {
            MensagemErro = "Erro ao carregar dados: " + ex.Message;
            _logger.LogError(ex, "Erro ao carregar dados do prontuário");
            return Page();
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            if (!await _configuracaoService.ObterValorBoolAsync("Prontuario.Habilitado", true))
            {
                MensagemErro = "O prontuário eletrônico está desabilitado.";
                Pacientes = await _pacienteService.GetAllAsync();
                Psicologos = await _psicologoService.GetAllAsync();
                return Page();
            }

            Pacientes = await _pacienteService.GetAllAsync();
            Psicologos = await _psicologoService.GetAllAsync();

            if (!ModelState.IsValid)
                return Page();

            var psicologoLogado = await ObterPsicologoLogadoAsync();
            if (!User.IsInRole("Admin"))
            {
                if (psicologoLogado == null)
                {
                    MensagemErro = "Psicólogo não identificado.";
                    return Page();
                }
                Prontuario.PsicologoId = psicologoLogado.Id;
            }

            if (Prontuario.Id > 0)
            {
                var existente = await _prontuarioService.ObterPorIdAsync(Prontuario.Id);
                if (existente == null)
                    return NotFound();
                if (!await PodeAcessarAsync(existente))
                    return Forbid();
                if (existente.Finalizado)
                {
                    MensagemErro = "Prontuário finalizado não pode ser editado.";
                    return Page();
                }
            }

            if (!string.IsNullOrEmpty(NovaEvolucao))
            {
                if (string.IsNullOrEmpty(Prontuario.Evolucao))
                    Prontuario.Evolucao = NovaEvolucao;
                else
                    Prontuario.Evolucao += "\n---\n" + NovaEvolucao;
            }

            if (ArquivoAnexo != null && ArquivoAnexo.Any())
            {
                var anexos = ArquivoAnexo.Where(a => a.Length > 0).Select(a => Path.GetFileName(a.FileName)).ToList();
                if (anexos.Any())
                    Prontuario.Anexos = string.Join(", ", anexos);
            }

            if (Prontuario.Id == 0)
            {
                Prontuario.DataCriacao = DateTime.Now;
                Prontuario.DataAtualizacao = DateTime.Now;
                await _prontuarioService.CriarProntuarioAsync(Prontuario);
                TempData["SuccessMessage"] = "Prontuário criado com sucesso!";
            }
            else
            {
                Prontuario.DataAtualizacao = DateTime.Now;
                await _prontuarioService.AtualizarProntuarioAsync(Prontuario);
                TempData["SuccessMessage"] = "Prontuário atualizado com sucesso!";
            }

            return RedirectToPage("Index");
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "Erro ao salvar prontuário: " + ex.Message;
            _logger.LogError(ex, "Erro ao salvar prontuário");
            Pacientes = await _pacienteService.GetAllAsync();
            Psicologos = await _psicologoService.GetAllAsync();
            MensagemErro = TempData["ErrorMessage"]?.ToString();
            return Page();
        }
    }
}
