using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ClinicaPsi.Shared.Models;
using ClinicaPsi.Application.Services;

namespace ClinicaPsi.Web.Pages.Cliente;

[Authorize(Roles = "Cliente")]
public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ConsultaService _consultaService;
    private readonly PacienteService _pacienteService;

    public IndexModel(
        UserManager<ApplicationUser> userManager,
        ConsultaService consultaService,
        PacienteService pacienteService)
    {
        _userManager = userManager;
        _consultaService = consultaService;
        _pacienteService = pacienteService;
    }

    public int PsicoPontos { get; set; }
    public int ConsultasRealizadas { get; set; }
    public int ConsultasGratuitas { get; set; }
    public List<Consulta> ProximasConsultas { get; set; } = new();
    public List<Consulta> HistoricoConsultas { get; set; } = new();
    public ClinicaPsi.Shared.Models.Paciente? PacienteInfo { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            // Obter usuário atual
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToPage("/Account/Login");
            }

            // Se não tem PacienteId associado, tentar encontrar por email
            if (usuario.PacienteId == null)
            {
                var pacientesPorEmail = await _pacienteService.GetAllAsync();
                var paciente = pacientesPorEmail.FirstOrDefault(p => p.Email == usuario.Email);
                if (paciente != null)
                {
                    usuario.PacienteId = paciente.Id;
                    await _userManager.UpdateAsync(usuario);
                }
            }

            // Se ainda não tem PacienteId, criar valores padrão
            if (usuario.PacienteId == null)
            {
                PsicoPontos = 0;
                ConsultasRealizadas = 0;
                ConsultasGratuitas = 0;
                PacienteInfo = null;
            }
            else
            {
                // Obter dados do paciente
                PacienteInfo = await _pacienteService.GetByIdAsync(usuario.PacienteId.Value);
                if (PacienteInfo != null)
                {
                    PsicoPontos = PacienteInfo.PsicoPontos;
                    ConsultasRealizadas = PacienteInfo.ConsultasRealizadas;
                    ConsultasGratuitas = PacienteInfo.ConsultasGratuitas;
                }
            }

            // Carregar próximas consultas
            var todasConsultas = await _consultaService.GetAllAsync();
            ProximasConsultas = usuario.PacienteId.HasValue ? 
                todasConsultas
                    .Where(c => c.PacienteId == usuario.PacienteId && 
                               c.DataHorario > DateTime.Now &&
                               c.Status == StatusConsulta.Agendada)
                    .OrderBy(c => c.DataHorario)
                    .Take(5)
                    .ToList() : new List<Consulta>();

            // Carregar histórico de consultas
            HistoricoConsultas = usuario.PacienteId.HasValue ? 
                todasConsultas
                    .Where(c => c.PacienteId == usuario.PacienteId && 
                               c.Status == StatusConsulta.Realizada)
                    .OrderByDescending(c => c.DataHorario)
                    .Take(10)
                    .ToList() : new List<Consulta>();

            return Page();
        }
        catch (Exception ex)
        {
            TempData["Erro"] = $"Erro ao carregar dados: {ex.Message}";
            return Page();
        }
    }
}