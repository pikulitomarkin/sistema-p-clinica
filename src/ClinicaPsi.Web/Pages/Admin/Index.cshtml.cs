using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ClinicaPsi.Shared.Models;
using ClinicaPsi.Application.Services;

namespace ClinicaPsi.Web.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly PacienteService _pacienteService;
        private readonly ConsultaService _consultaService;
        private readonly PsicologoService _psicologoService;

        public IndexModel(
            UserManager<ApplicationUser> userManager,
            PacienteService pacienteService,
            ConsultaService consultaService,
            PsicologoService psicologoService)
        {
            _userManager = userManager;
            _pacienteService = pacienteService;
            _consultaService = consultaService;
            _psicologoService = psicologoService;
        }

        public int TotalPacientes { get; set; }
        public int TotalPsicologos { get; set; }
        public int ConsultasHoje { get; set; }
        public decimal ReceitaMensal { get; set; }
        public List<ActivityLog> UltimasAtividades { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                // Carregar estatísticas
                var pacientes = await _pacienteService.GetAllAsync();
                TotalPacientes = pacientes.Count();

                var psicologos = await _psicologoService.GetAllAsync();
                TotalPsicologos = psicologos.Count();

                var consultasHoje = await _consultaService.GetConsultasByDateAsync(DateTime.Today);
                ConsultasHoje = consultasHoje.Count();

                // Calcular receita mensal (consultas realizadas no mês atual)
                var inicioMes = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var consultasRealizadas = await _consultaService.GetConsultasRealizadasAsync(inicioMes, DateTime.Now);
                ReceitaMensal = consultasRealizadas.Sum(c => c.Valor);

                // Simular últimas atividades (posteriormente será implementado um log real)
                UltimasAtividades = new List<ActivityLog>
                {
                    new ActivityLog 
                    { 
                        Data = DateTime.Now.AddHours(-2), 
                        Descricao = "Novo paciente cadastrado: Maria Silva",
                        Tipo = "NovoUsuario",
                        Usuario = "Sistema"
                    },
                    new ActivityLog 
                    { 
                        Data = DateTime.Now.AddHours(-3), 
                        Descricao = "Consulta agendada para João Santos",
                        Tipo = "NovaConsulta",
                        Usuario = "Dra. Ana Santos"
                    },
                    new ActivityLog 
                    { 
                        Data = DateTime.Now.AddDays(-1), 
                        Descricao = "Cliente resgatou consulta gratuita com PsicoPontos",
                        Tipo = "PsicoPontos",
                        Usuario = "Sistema"
                    }
                };

                return Page();
            }
            catch (Exception ex)
            {
                // Log do erro (implementar logger posteriormente)
                ModelState.AddModelError("", "Erro ao carregar dados do dashboard: " + ex.Message);
                return Page();
            }
        }
    }

    public class ActivityLog
    {
        public DateTime Data { get; set; }
        public string Descricao { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public string Usuario { get; set; } = string.Empty;
    }
}