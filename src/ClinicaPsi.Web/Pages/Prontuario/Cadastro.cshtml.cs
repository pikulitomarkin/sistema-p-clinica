using ClinicaPsi.Application.Services;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

[Authorize(Roles = "Admin,Psicologo")]
public class CadastroModel : PageModel
{
    private readonly ProntuarioService _prontuarioService;
    private readonly PacienteService _pacienteService;
    private readonly PsicologoService _psicologoService;

    public CadastroModel(ProntuarioService prontuarioService, PacienteService pacienteService, PsicologoService psicologoService)
    {
        _prontuarioService = prontuarioService;
        _pacienteService = pacienteService;
        _psicologoService = psicologoService;
    }

    [BindProperty]
    public ProntuarioEletronico Prontuario { get; set; } = new();
    public List<Paciente> Pacientes { get; set; } = new();
    public List<Psicologo> Psicologos { get; set; } = new();
    public bool Edicao => Prontuario.Id > 0;

    [BindProperty]
    public List<IFormFile>? ArquivoAnexo { get; set; }

    public async Task OnGetAsync(int? id)
    {
        Pacientes = await _pacienteService.GetAllAsync();
        Psicologos = await _psicologoService.GetAllAsync();
        if (id.HasValue)
        {
            var prontuario = await _prontuarioService.ObterPorIdAsync(id.Value);
            if (prontuario != null)
                Prontuario = prontuario;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            Pacientes = await _pacienteService.GetAllAsync();
            Psicologos = await _psicologoService.GetAllAsync();
            return Page();
        }
        // Salvar anexos (implementação simplificada)
        if (ArquivoAnexo != null && ArquivoAnexo.Any())
        {
            Prontuario.Anexos = string.Join(", ", ArquivoAnexo.Select(f => f.FileName));
            // Aqui você pode salvar os arquivos fisicamente se desejar
        }
        if (Edicao)
            await _prontuarioService.AtualizarProntuarioAsync(Prontuario);
        else
            await _prontuarioService.CriarProntuarioAsync(Prontuario);
        return RedirectToPage("/Prontuario/Index");
    }
}
