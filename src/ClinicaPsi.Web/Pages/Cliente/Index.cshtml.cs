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

    public int ConsultasRealizadas { get; set; }
    public int ConsultasAgendadas { get; set; }
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
                ConsultasRealizadas = 0;
                ConsultasAgendadas = 0;
                PacienteInfo = null;
            }
            else
            {
                PacienteInfo = await _pacienteService.GetByIdAsync(usuario.PacienteId.Value);
            }

            var todasConsultas = await _consultaService.GetAllAsync();
            var minhas = usuario.PacienteId.HasValue
                ? todasConsultas.Where(c => c.PacienteId == usuario.PacienteId.Value).ToList()
                : new List<Consulta>();

            // Contadores a partir das consultas reais (não do campo desatualizado do paciente)
            ConsultasRealizadas = minhas.Count(c => c.Status == StatusConsulta.Realizada);
            ConsultasAgendadas = minhas.Count(c =>
                (c.Status == StatusConsulta.Agendada || c.Status == StatusConsulta.Confirmada) &&
                c.DataHorario.Date >= DateTime.Today);

            // Próximas: agendadas/confirmadas de hoje em diante
            ProximasConsultas = minhas
                .Where(c => (c.Status == StatusConsulta.Agendada || c.Status == StatusConsulta.Confirmada) &&
                            c.DataHorario.Date >= DateTime.Today)
                .OrderBy(c => c.DataHorario)
                .Take(5)
                .ToList();

            HistoricoConsultas = minhas
                .Where(c => c.Status == StatusConsulta.Realizada)
                .OrderByDescending(c => c.DataHorario)
                .Take(10)
                .ToList();

            return Page();
        }
        catch (Exception ex)
        {
            TempData["Erro"] = $"Erro ao carregar dados: {ex.Message}";
            return Page();
        }
    }
}