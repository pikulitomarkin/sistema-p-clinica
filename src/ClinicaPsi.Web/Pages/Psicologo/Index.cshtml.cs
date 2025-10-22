using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ClinicaPsi.Shared.Models;
using ClinicaPsi.Application.Services;

namespace ClinicaPsi.Web.Pages.Psicologo
{
    [Authorize(Roles = "Admin,Psicologo")]
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ConsultaService _consultaService;
        private readonly PacienteService _pacienteService;
        private readonly PsicologoService _psicologoService;

        public IndexModel(
            UserManager<ApplicationUser> userManager,
            ConsultaService consultaService,
            PacienteService pacienteService,
            PsicologoService psicologoService)
        {
            _userManager = userManager;
            _consultaService = consultaService;
            _pacienteService = pacienteService;
            _psicologoService = psicologoService;
        }

        public int ConsultasHoje { get; set; }
        public int ConsultasProximos7Dias { get; set; }
        public int TotalPacientes { get; set; }
        public int ConsultasRealizadas { get; set; }
        public List<Consulta> ProximasConsultas { get; set; } = new();
        public decimal? ValorConsulta { get; set; }
        public string? CRP { get; set; }
        public bool AtendeManha { get; set; }
        public bool AtendeTarde { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                // Debug: Verificar informações do usuário
                var userRoles = new List<string>();
                if (User.Identity?.IsAuthenticated == true)
                {
                    var currentUser = await _userManager.GetUserAsync(User);
                    if (currentUser != null)
                    {
                        userRoles = (await _userManager.GetRolesAsync(currentUser)).ToList();
                    }
                }

                // Se não for psicólogo nem admin, redirecionar
                if (!User.IsInRole("Psicologo") && !User.IsInRole("Admin"))
                {
                    // Para debug, vamos apenas adicionar uma mensagem
                    ViewData["DebugMessage"] = $"Usuário não tem role Psicologo ou Admin. Roles: {string.Join(", ", userRoles)}";
                }

                // Obter usuário atual
                var usuario = await _userManager.GetUserAsync(User);
                if (usuario == null)
                {
                    return RedirectToPage("/Account/Login");
                }

                // Se não tem PsicologoId associado, tentar encontrar por email
                if (usuario.PsicologoId == null)
                {
                    var psicologoPorEmail = await _psicologoService.GetAllAsync();
                    var psicologo = psicologoPorEmail.FirstOrDefault(p => p.Email == usuario.Email);
                    if (psicologo != null)
                    {
                        usuario.PsicologoId = psicologo.Id;
                        await _userManager.UpdateAsync(usuario);
                    }
                }

                // Se ainda não tem PsicologoId, criar valores padrão
                if (usuario.PsicologoId == null)
                {
                    ValorConsulta = 150m;
                    CRP = "Não informado";
                    AtendeManha = true;
                    AtendeTarde = true;
                }
                else
                {
                    // Obter dados do psicólogo
                    var psicologo = await _psicologoService.GetByIdAsync(usuario.PsicologoId.Value);
                    if (psicologo != null)
                    {
                        ValorConsulta = psicologo.ValorConsulta;
                        CRP = psicologo.CRP;
                        // Verificar se atende manhã e tarde baseado nos horários
                        AtendeManha = psicologo.HorarioInicioManha < psicologo.HorarioFimManha;
                        AtendeTarde = psicologo.HorarioInicioTarde < psicologo.HorarioFimTarde;
                    }
                }

                // Carregar estatísticas
                var consultasHoje = await _consultaService.GetConsultasByDateAsync(DateTime.Today);
                ConsultasHoje = usuario.PsicologoId.HasValue ? 
                    consultasHoje.Where(c => c.PsicologoId == usuario.PsicologoId).Count() : 0;

                var proximasConsultas = await _consultaService.GetProximasConsultasAsync(7);
                ConsultasProximos7Dias = usuario.PsicologoId.HasValue ? 
                    proximasConsultas.Where(c => c.PsicologoId == usuario.PsicologoId).Count() : 0;
                ProximasConsultas = usuario.PsicologoId.HasValue ? 
                    proximasConsultas.Where(c => c.PsicologoId == usuario.PsicologoId).ToList() : new List<Consulta>();

                // Consultas realizadas no mês atual
                var inicioMes = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var consultasRealizadas = await _consultaService.GetConsultasRealizadasAsync(inicioMes, DateTime.Now);
                ConsultasRealizadas = usuario.PsicologoId.HasValue ? 
                    consultasRealizadas.Where(c => c.PsicologoId == usuario.PsicologoId).Count() : 0;

                // Total de pacientes (aproximação - seria melhor ter uma relação direta)
                var todasConsultas = await _consultaService.GetAllAsync();
                TotalPacientes = usuario.PsicologoId.HasValue ? 
                    todasConsultas
                        .Where(c => c.PsicologoId == usuario.PsicologoId)
                        .Select(c => c.PacienteId)
                        .Distinct()
                        .Count() : 0;

                return Page();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Erro ao carregar dados: " + ex.Message);
                return Page();
            }
        }
    }
}