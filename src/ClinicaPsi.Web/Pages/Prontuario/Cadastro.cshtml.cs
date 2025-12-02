using ClinicaPsi.Application.Services;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

[Authorize(Roles = "Admin,Psicologo")]
public class CadastroModel : PageModel
{
    private readonly ProntuarioService _prontuarioService;
    private readonly PacienteService _pacienteService;
    private readonly PsicologoService _psicologoService;
    private readonly ConsultaService _consultaService;
    private readonly ILogger<CadastroModel> _logger;

    public CadastroModel(
        ProntuarioService prontuarioService,
        PacienteService pacienteService,
        PsicologoService psicologoService,
        ConsultaService consultaService,
        ILogger<CadastroModel> logger)
    {
        _prontuarioService = prontuarioService;
        _pacienteService = pacienteService;
        _psicologoService = psicologoService;
        _consultaService = consultaService;
        _logger = logger;
    }

    [BindProperty]
    public ProntuarioEletronico Prontuario { get; set; } = new();

    public List<Paciente> Pacientes { get; set; } = new();
    public List<Psicologo> Psicologos { get; set; } = new();
    public List<Consulta> Consultas { get; set; } = new();

    public bool Edicao => Prontuario.Id > 0;
    public string? MensagemErro { get; set; }
    public string? MensagemSucesso { get; set; }

    [BindProperty]
    public List<IFormFile>? ArquivoAnexo { get; set; }

    [BindProperty]
    public string? NovaEvolucao { get; set; }

    public async Task OnGetAsync(int? id)
    {
        try
        {
            Pacientes = await _pacienteService.GetAllAsync();
            Psicologos = await _psicologoService.GetAllAsync();

            if (id.HasValue)
            {
                var prontuario = await _prontuarioService.ObterPorIdAsync(id.Value);
                if (prontuario != null)
                {
                    Prontuario = prontuario;
                }
                else
                {
                    MensagemErro = "Prontuário não encontrado.";
                }
            }
        }
        catch (Exception ex)
        {
            MensagemErro = "Erro ao carregar dados: " + ex.Message;
            _logger.LogError(ex, "Erro ao carregar dados do prontuário");
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            if (!ModelState.IsValid)
            {
                Pacientes = await _pacienteService.GetAllAsync();
                Psicologos = await _psicologoService.GetAllAsync();
                return Page();
            }

            int psicologoId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            
            // Adicionar nova evolução se fornecida
            if (!string.IsNullOrEmpty(NovaEvolucao))
            {
                if (string.IsNullOrEmpty(Prontuario.Evolucao))
                    Prontuario.Evolucao = NovaEvolucao;
                else
                    Prontuario.Evolucao += "\n---\n" + NovaEvolucao;
            }

            // Processar anexos
            if (ArquivoAnexo != null && ArquivoAnexo.Any())
            {
                var anexos = new List<string>();
                foreach (var arquivo in ArquivoAnexo)
                {
                    if (arquivo.Length > 0)
                    {
                        var nomeArquivo = Path.GetFileName(arquivo.FileName);
                        anexos.Add(nomeArquivo);
                        _logger.LogInformation($"Arquivo anexado: {nomeArquivo}");
                    }
                }
                if (anexos.Any())
                {
                    Prontuario.Anexos = string.Join(", ", anexos);
                }
            }

            // Definir dados automáticos
            if (Prontuario.Id == 0)
            {
                Prontuario.PsicologoId = psicologoId;
                Prontuario.DataCriacao = DateTime.Now;
                await _prontuarioService.CriarProntuarioAsync(Prontuario);
                TempData["SuccessMessage"] = "Prontuário criado com sucesso!";
                _logger.LogInformation($"Prontuário criado com sucesso - ID: {Prontuario.Id}");
            }
            else
            {
                Prontuario.DataAtualizacao = DateTime.Now;
                await _prontuarioService.AtualizarProntuarioAsync(Prontuario);
                TempData["SuccessMessage"] = "Prontuário atualizado com sucesso!";
                _logger.LogInformation($"Prontuário {Prontuario.Id} atualizado com sucesso");
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

    public async Task<IActionResult> OnPostAdicionarEvolucaoAsync(int id)
    {
        try
        {
            var prontuario = await _prontuarioService.ObterPorIdAsync(id);
            if (prontuario == null)
                return NotFound();

            if (!string.IsNullOrEmpty(NovaEvolucao))
            {
                if (string.IsNullOrEmpty(prontuario.Evolucao))
                    prontuario.Evolucao = NovaEvolucao;
                else
                    prontuario.Evolucao += "\n---\n" + NovaEvolucao;

                prontuario.DataAtualizacao = DateTime.Now;
                await _prontuarioService.AtualizarProntuarioAsync(prontuario);
                MensagemSucesso = "Evolução adicionada com sucesso!";
            }

            return RedirectToPage("Detalhe", new { id });
        }
        catch (Exception ex)
        {
            MensagemErro = "Erro ao adicionar evolução: " + ex.Message;
            _logger.LogError(ex, "Erro ao adicionar evolução");
            return Page();
        }
    }
}
