using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;

namespace ClinicaPsi.Web.Pages.Admin
{
    [Authorize]
    public class ConsultasModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ConsultasModel(AppDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [BindProperty(SupportsGet = true)]
        public string? BuscaNome { get; set; }

        [BindProperty(SupportsGet = true)]
        public StatusConsulta? FiltroStatus { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? DataInicio { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? DataFim { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? OrdenarPor { get; set; } = "DataHorario";

        [BindProperty(SupportsGet = true)]
        public string? Direcao { get; set; } = "desc";

        [BindProperty(SupportsGet = true)]
        public int PaginaAtual { get; set; } = 1;

        public int TotalPaginas { get; set; }
        public List<Consulta> Consultas { get; set; } = new();
        
        // Estatísticas
        public int TotalConsultas { get; set; }
        public int ConsultasAgendadas { get; set; }
        public int ConsultasRealizadas { get; set; }
        public int ConsultasCanceladas { get; set; }
        
        // Valores
        public decimal FaturamentoTotal { get; set; }
        public decimal FaturamentoMes { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !await _userManager.IsInRoleAsync(user, "Admin"))
            {
                return Forbid();
            }

            await CarregarEstatisticasAsync();
            await CarregarConsultasAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostCancelarConsultaAsync(int consultaId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !await _userManager.IsInRoleAsync(user, "Admin"))
            {
                return Forbid();
            }

            var consulta = await _context.Consultas.FindAsync(consultaId);
            if (consulta != null && consulta.Status == StatusConsulta.Agendada)
            {
                consulta.Status = StatusConsulta.Cancelada;
                consulta.DataAtualizacao = DateTime.Now;
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Consulta cancelada com sucesso!";
            }
            else
            {
                TempData["ErrorMessage"] = "Não foi possível cancelar a consulta.";
            }

            return RedirectToPage();
        }

        private async Task CarregarEstatisticasAsync()
        {
            var inicioMes = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            
            TotalConsultas = await _context.Consultas.CountAsync();
            ConsultasAgendadas = await _context.Consultas.CountAsync(c => c.Status == StatusConsulta.Agendada);
            ConsultasRealizadas = await _context.Consultas.CountAsync(c => c.Status == StatusConsulta.Realizada);
            ConsultasCanceladas = await _context.Consultas.CountAsync(c => c.Status == StatusConsulta.Cancelada);
            
            FaturamentoTotal = await _context.Consultas
                .Where(c => c.Status == StatusConsulta.Realizada)
                .SumAsync(c => c.Valor);
                
            FaturamentoMes = await _context.Consultas
                .Where(c => c.Status == StatusConsulta.Realizada && c.DataHorario >= inicioMes)
                .SumAsync(c => c.Valor);
        }

        private async Task CarregarConsultasAsync()
        {
            var query = _context.Consultas
                .Include(c => c.Paciente)
                .Include(c => c.Psicologo)
                .AsQueryable();

            // Filtros
            if (!string.IsNullOrWhiteSpace(BuscaNome))
            {
                query = query.Where(c => c.Paciente.Nome.Contains(BuscaNome) || 
                                       c.Psicologo.Nome.Contains(BuscaNome));
            }

            if (FiltroStatus.HasValue)
            {
                query = query.Where(c => c.Status == FiltroStatus.Value);
            }

            if (DataInicio.HasValue)
            {
                query = query.Where(c => c.DataHorario.Date >= DataInicio.Value.Date);
            }

            if (DataFim.HasValue)
            {
                query = query.Where(c => c.DataHorario.Date <= DataFim.Value.Date);
            }

            // Ordenação
            switch (OrdenarPor?.ToLower())
            {
                case "paciente":
                    query = Direcao == "asc" ? query.OrderBy(c => c.Paciente.Nome) : query.OrderByDescending(c => c.Paciente.Nome);
                    break;
                case "psicologo":
                    query = Direcao == "asc" ? query.OrderBy(c => c.Psicologo.Nome) : query.OrderByDescending(c => c.Psicologo.Nome);
                    break;
                case "status":
                    query = Direcao == "asc" ? query.OrderBy(c => c.Status) : query.OrderByDescending(c => c.Status);
                    break;
                case "valor":
                    query = Direcao == "asc" ? query.OrderBy(c => c.Valor) : query.OrderByDescending(c => c.Valor);
                    break;
                default:
                    query = Direcao == "asc" ? query.OrderBy(c => c.DataHorario) : query.OrderByDescending(c => c.DataHorario);
                    break;
            }

            // Paginação
            const int itensPorPagina = 20;
            var totalItens = await query.CountAsync();
            TotalPaginas = (int)Math.Ceiling((double)totalItens / itensPorPagina);

            Consultas = await query
                .Skip((PaginaAtual - 1) * itensPorPagina)
                .Take(itensPorPagina)
                .ToListAsync();
        }
    }
}