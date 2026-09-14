using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using System.Globalization;
using System.Security.Claims;

namespace ClinicaPsi.Web.Pages.Psicologo
{
    [Authorize(Roles = "Admin,Psicologo")]
    public class RelatorioMensalModel : PageModel
    {
        private readonly AppDbContext _context;
        private static readonly CultureInfo CulturaPtBr = new("pt-BR");

        public RelatorioMensalModel(AppDbContext context)
        {
            _context = context;
        }

        public int AnoSelecionado { get; set; }
        public int MesSelecionado { get; set; }
        public int? FiltroPsicologoId { get; set; }
        public bool EhAdmin { get; set; }
        public string NomePsicologoFiltro { get; set; } = "Todos";

        public int TotalConsultas { get; set; }
        public int ConsultasAgendadas { get; set; }
        public int ConsultasRealizadas { get; set; }
        public int ConsultasCanceladas { get; set; }
        public int ConsultasNoShow { get; set; }
        public int PacientesAtendidos { get; set; }
        public decimal ReceitaMes { get; set; }

        public List<ConsultaResumoDto> Consultas { get; set; } = new();
        public List<SelectListItem> AnosDisponiveis { get; set; } = new();
        public List<SelectListItem> MesesDisponiveis { get; set; } = new();
        public List<SelectListItem> PsicologosDisponiveis { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int? ano = null, int? mes = null, int? psicologoId = null)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                    return Forbid();

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                EhAdmin = User.IsInRole("Admin");

                int? escopoPsicologoId;
                if (EhAdmin)
                {
                    escopoPsicologoId = psicologoId;
                    FiltroPsicologoId = psicologoId;
                }
                else if (user?.PsicologoId == null)
                {
                    return Forbid();
                }
                else
                {
                    escopoPsicologoId = user.PsicologoId.Value;
                    FiltroPsicologoId = escopoPsicologoId;
                }

                var hoje = DateTime.Today;
                AnoSelecionado = ano is >= 2000 and <= 2100 ? ano.Value : hoje.Year;
                MesSelecionado = mes is >= 1 and <= 12 ? mes.Value : hoje.Month;

                var inicioMes = new DateTime(AnoSelecionado, MesSelecionado, 1);
                var fimMes = inicioMes.AddMonths(1).AddTicks(-1);

                await CarregarFiltrosAsync();

                var query = _context.Consultas
                    .AsNoTracking()
                    .Include(c => c.Paciente)
                    .Include(c => c.Psicologo)
                    .Where(c => c.DataHorario >= inicioMes && c.DataHorario <= fimMes);

                if (escopoPsicologoId.HasValue)
                {
                    query = query.Where(c => c.PsicologoId == escopoPsicologoId.Value);
                    NomePsicologoFiltro = PsicologosDisponiveis
                        .FirstOrDefault(p => p.Value == escopoPsicologoId.Value.ToString())?.Text
                        ?? "Psicólogo";
                }
                else
                {
                    NomePsicologoFiltro = "Todos os psicólogos";
                }

                var consultasMes = await query
                    .OrderBy(c => c.DataHorario)
                    .ToListAsync();

                TotalConsultas = consultasMes.Count;
                ConsultasAgendadas = consultasMes.Count(c =>
                    c.Status == StatusConsulta.Agendada ||
                    c.Status == StatusConsulta.Confirmada ||
                    c.Status == StatusConsulta.Reagendada);
                ConsultasRealizadas = consultasMes.Count(c => c.Status == StatusConsulta.Realizada);
                ConsultasCanceladas = consultasMes.Count(c => c.Status == StatusConsulta.Cancelada);
                ConsultasNoShow = consultasMes.Count(c => c.Status == StatusConsulta.NoShow);
                PacientesAtendidos = consultasMes
                    .Where(c => c.Status == StatusConsulta.Realizada)
                    .Select(c => c.PacienteId)
                    .Distinct()
                    .Count();
                ReceitaMes = consultasMes
                    .Where(c => c.Status == StatusConsulta.Realizada)
                    .Sum(c => c.Valor);

                Consultas = consultasMes
                    .Select(c => new ConsultaResumoDto
                    {
                        Id = c.Id,
                        DataHorario = c.DataHorario,
                        PacienteNome = c.Paciente?.Nome ?? "—",
                        PsicologoNome = c.Psicologo?.Nome ?? "—",
                        Status = c.Status,
                        Valor = c.Valor,
                        Formato = c.Formato
                    })
                    .ToList();

                return Page();
            }
            catch (Exception)
            {
                TempData["Error"] = "Não foi possível carregar o relatório mensal. Tente novamente.";
                return Page();
            }
        }

        private async Task CarregarFiltrosAsync()
        {
            var anoAtual = DateTime.Today.Year;
            AnosDisponiveis = Enumerable.Range(anoAtual - 4, 6)
                .Select(a => new SelectListItem
                {
                    Value = a.ToString(),
                    Text = a.ToString(),
                    Selected = a == AnoSelecionado
                })
                .ToList();

            MesesDisponiveis = Enumerable.Range(1, 12)
                .Select(m => new SelectListItem
                {
                    Value = m.ToString(),
                    Text = CulturaPtBr.DateTimeFormat.GetMonthName(m),
                    Selected = m == MesSelecionado
                })
                .ToList();

            if (EhAdmin)
            {
                var psicologos = await _context.Psicologos
                    .AsNoTracking()
                    .OrderBy(p => p.Nome)
                    .Select(p => new { p.Id, p.Nome })
                    .ToListAsync();

                PsicologosDisponiveis = new List<SelectListItem>
                {
                    new() { Value = "", Text = "Todos", Selected = !FiltroPsicologoId.HasValue }
                };
                PsicologosDisponiveis.AddRange(psicologos.Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = p.Nome,
                    Selected = FiltroPsicologoId == p.Id
                }));
            }
        }
    }

    public class ConsultaResumoDto
    {
        public int Id { get; set; }
        public DateTime DataHorario { get; set; }
        public string PacienteNome { get; set; } = string.Empty;
        public string PsicologoNome { get; set; } = string.Empty;
        public StatusConsulta Status { get; set; }
        public decimal Valor { get; set; }
        public FormatoConsulta Formato { get; set; }
    }
}
