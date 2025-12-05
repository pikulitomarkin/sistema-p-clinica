using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;

namespace ClinicaPsi.Web.Pages.Admin.Usuarios
{
    [Authorize(Roles = "Admin")]
    public class DetalhesModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DetalhesModel(
            AppDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public ApplicationUser Usuario { get; set; } = null!;
        public Paciente? Paciente { get; set; }
        public ClinicaPsi.Shared.Models.Psicologo? Psicologo { get; set; }
        public List<string> Roles { get; set; } = new();
        public int TotalConsultas { get; set; }
        public int ConsultasRealizadas { get; set; }
        public int ConsultasAgendadas { get; set; }
        public DateTime? UltimaConsulta { get; set; }
        public DateTime? ProximaConsulta { get; set; }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            Usuario = await _userManager.FindByIdAsync(id);
            if (Usuario == null)
            {
                return NotFound();
            }

            // Carregar roles do usuário
            Roles = (await _userManager.GetRolesAsync(Usuario)).ToList();

            // Se for paciente, carregar dados do paciente
            if (Usuario.TipoUsuario == TipoUsuario.Cliente)
            {
                Paciente = await _context.Pacientes
                    .FirstOrDefaultAsync(p => p.Id == Usuario.PacienteId);

                if (Paciente != null)
                {
                    // Carregar estatísticas de consultas
                    var consultas = await _context.Consultas
                        .Where(c => c.PacienteId == Paciente.Id)
                        .ToListAsync();

                    TotalConsultas = consultas.Count;
                    ConsultasRealizadas = consultas.Count(c => c.Status == StatusConsulta.Realizada);
                    ConsultasAgendadas = consultas.Count(c =>
                        c.Status == StatusConsulta.Agendada ||
                        c.Status == StatusConsulta.Confirmada);

                    UltimaConsulta = consultas
                        .Where(c => c.Status == StatusConsulta.Realizada)
                        .OrderByDescending(c => c.DataHorario)
                        .FirstOrDefault()?.DataHorario;

                    ProximaConsulta = consultas
                        .Where(c => c.Status == StatusConsulta.Agendada || c.Status == StatusConsulta.Confirmada)
                        .OrderBy(c => c.DataHorario)
                        .FirstOrDefault()?.DataHorario;
                }
            }

            // Se for psicólogo, carregar dados do psicólogo
            if (Usuario.TipoUsuario == TipoUsuario.Psicologo)
            {
                Psicologo = await _context.Psicologos
                    .FirstOrDefaultAsync(p => p.UserId == id);

                if (Psicologo != null)
                {
                    // Carregar estatísticas de consultas
                    var consultas = await _context.Consultas
                        .Where(c => c.PsicologoId == Psicologo.Id)
                        .ToListAsync();

                    TotalConsultas = consultas.Count;
                    ConsultasRealizadas = consultas.Count(c => c.Status == StatusConsulta.Realizada);
                    ConsultasAgendadas = consultas.Count(c =>
                        c.Status == StatusConsulta.Agendada ||
                        c.Status == StatusConsulta.Confirmada);

                    UltimaConsulta = consultas
                        .Where(c => c.Status == StatusConsulta.Realizada)
                        .OrderByDescending(c => c.DataHorario)
                        .FirstOrDefault()?.DataHorario;

                    ProximaConsulta = consultas
                        .Where(c => c.Status == StatusConsulta.Agendada || c.Status == StatusConsulta.Confirmada)
                        .OrderBy(c => c.DataHorario)
                        .FirstOrDefault()?.DataHorario;
                }
            }

            return Page();
        }
    }
}
