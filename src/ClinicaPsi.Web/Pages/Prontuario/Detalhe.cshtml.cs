using ClinicaPsi.Application.Services;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace ClinicaPsi.Web.Pages.Prontuario;

[Authorize(Roles = "Admin,Psicologo")]
public class DetalheModel : PageModel
{
    private readonly ProntuarioService _prontuarioService;
    private readonly PacienteService _pacienteService;
    private readonly PsicologoService _psicologoService;
    private readonly ConsultaService _consultaService;
    private readonly ILogger<DetalheModel> _logger;

    public DetalheModel(
        ProntuarioService prontuarioService,
        PacienteService pacienteService,
        PsicologoService psicologoService,
        ConsultaService consultaService,
        ILogger<DetalheModel> logger)
    {
        _prontuarioService = prontuarioService;
        _pacienteService = pacienteService;
        _psicologoService = psicologoService;
        _consultaService = consultaService;
        _logger = logger;
    }

    public ProntuarioEletronico? Prontuario { get; set; }
    public Paciente? Paciente { get; set; }
    public ClinicaPsi.Shared.Models.Psicologo? Psicologo { get; set; }
    public List<Consulta> ConsultasRelacionadas { get; set; } = new();
    public string? MensagemErro { get; set; }
    public string? MensagemSucesso { get; set; }

    [BindProperty]
    public string? NovaEvolucao { get; set; }

    public async Task OnGetAsync(int id)
    {
        try
        {
            Prontuario = await _prontuarioService.ObterPorIdAsync(id);
            
            if (Prontuario != null)
            {
                // Carregar dados relacionados
                Paciente = await _pacienteService.GetByIdAsync(Prontuario.PacienteId);
                Psicologo = await _psicologoService.GetByIdAsync(Prontuario.PsicologoId);
                
                _logger.LogInformation($"Prontuário {id} carregado com sucesso");
            }
            else
            {
                MensagemErro = "Prontuário não encontrado.";
            }
        }
        catch (Exception ex)
        {
            MensagemErro = "Erro ao carregar prontuário: " + ex.Message;
            _logger.LogError(ex, "Erro ao carregar prontuário");
        }
    }

    public async Task<IActionResult> OnPostFinalizarAsync(int id)
    {
        try
        {
            var prontuario = await _prontuarioService.ObterPorIdAsync(id);
            if (prontuario == null)
                return NotFound();

            prontuario.Finalizado = true;
            prontuario.DataAtualizacao = DateTime.Now;
            await _prontuarioService.AtualizarProntuarioAsync(prontuario);
            
            MensagemSucesso = "Prontuário finalizado com sucesso!";
            _logger.LogInformation($"Prontuário {id} finalizado");
            
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
                return Page();
            }

            var prontuario = await _prontuarioService.ObterPorIdAsync(id);
            if (prontuario == null)
                return NotFound();

            var dataHora = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            var evolucaoComData = $"[{dataHora}]\n{NovaEvolucao}";

            if (string.IsNullOrEmpty(prontuario.Evolucao))
                prontuario.Evolucao = evolucaoComData;
            else
                prontuario.Evolucao += "\n\n---\n\n" + evolucaoComData;

            prontuario.DataAtualizacao = DateTime.Now;
            await _prontuarioService.AtualizarProntuarioAsync(prontuario);
            
            MensagemSucesso = "Evolução adicionada com sucesso!";
            _logger.LogInformation($"Evolução adicionada ao prontuário {id}");
            
            return RedirectToPage(new { id });
        }
        catch (Exception ex)
        {
            MensagemErro = "Erro ao adicionar evolução: " + ex.Message;
            _logger.LogError(ex, "Erro ao adicionar evolução");
            return Page();
        }
    }
}
