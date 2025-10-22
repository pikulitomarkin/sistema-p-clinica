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
    public class PsicoPontosModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public PsicoPontosModel(AppDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [BindProperty(SupportsGet = true)]
        public string? BuscaNome { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? FiltroMinPontos { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? OrdenarPor { get; set; } = "pontos";

        [BindProperty(SupportsGet = true)]
        public string? Direcao { get; set; } = "desc";

        public List<PacienteComPontos> PacientesLista { get; set; } = new();
        public List<HistoricoPontos> HistoricoRecente { get; set; } = new();
        
        // Estatísticas
        public int TotalPacientesAtivos { get; set; }
        public int QtdPacientesComPontos { get; set; }
        public int TotalPontosDistribuidos { get; set; }
        public int ConsultasGratuitasUsadas { get; set; }
        public int ConsultasGratuitasDisponiveis { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !await _userManager.IsInRoleAsync(user, "Admin"))
            {
                return Forbid();
            }

            await CarregarEstatisticasAsync();
            await CarregarPacientesComPontosAsync();
            await CarregarHistoricoRecenteAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostAdicionarPontosAsync(int pacienteId, int pontos, string motivo)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !await _userManager.IsInRoleAsync(user, "Admin"))
            {
                return Forbid();
            }

            if (pontos <= 0 || string.IsNullOrWhiteSpace(motivo))
            {
                TempData["ErrorMessage"] = "Pontos devem ser maior que zero e motivo deve ser informado.";
                return RedirectToPage();
            }

            var paciente = await _context.Pacientes.FindAsync(pacienteId);
            if (paciente == null)
            {
                TempData["ErrorMessage"] = "Paciente não encontrado.";
                return RedirectToPage();
            }

            // Adicionar pontos ao paciente
            paciente.PsicoPontos += pontos;
            paciente.DataAtualizacao = DateTime.Now;

            // Registrar no histórico
            var historico = new HistoricoPontos
            {
                PacienteId = pacienteId,
                PontosAlterados = pontos,
                Pontos = pontos,
                Motivo = motivo,
                Descricao = $"Pontos adicionados por {user.NomeCompleto ?? user.Email!}",
                TipoMovimentacao = TipoMovimentacaoPontos.Ganho,
                DataMovimentacao = DateTime.Now
            };

            _context.HistoricoPontos.Add(historico);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"{pontos} pontos adicionados para {paciente.Nome} com sucesso!";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRemoverPontosAsync(int pacienteId, int pontos, string motivo)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !await _userManager.IsInRoleAsync(user, "Admin"))
            {
                return Forbid();
            }

            if (pontos <= 0 || string.IsNullOrWhiteSpace(motivo))
            {
                TempData["ErrorMessage"] = "Pontos devem ser maior que zero e motivo deve ser informado.";
                return RedirectToPage();
            }

            var paciente = await _context.Pacientes.FindAsync(pacienteId);
            if (paciente == null)
            {
                TempData["ErrorMessage"] = "Paciente não encontrado.";
                return RedirectToPage();
            }

            if (paciente.PsicoPontos < pontos)
            {
                TempData["ErrorMessage"] = $"Paciente possui apenas {paciente.PsicoPontos} pontos. Não é possível remover {pontos} pontos.";
                return RedirectToPage();
            }

            // Remover pontos do paciente
            var pontosAntes = paciente.PsicoPontos;
            paciente.PsicoPontos -= pontos;
            paciente.DataAtualizacao = DateTime.Now;

            // Registrar no histórico
            var historico = new HistoricoPontos
            {
                PacienteId = pacienteId,
                PontosAlterados = -pontos, // Valor negativo para remoção
                Pontos = pontos,
                Motivo = motivo,
                Descricao = $"Pontos removidos por {user.NomeCompleto ?? user.Email!}",
                TipoMovimentacao = TipoMovimentacaoPontos.Uso,
                DataMovimentacao = DateTime.Now
            };

            _context.HistoricoPontos.Add(historico);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"{pontos} pontos removidos de {paciente.Nome} com sucesso!";
            return RedirectToPage();
        }

        private async Task CarregarEstatisticasAsync()
        {
            TotalPacientesAtivos = await _context.Pacientes.CountAsync(p => p.Ativo);
            QtdPacientesComPontos = await _context.Pacientes.CountAsync(p => p.Ativo && p.PsicoPontos > 0);
            TotalPontosDistribuidos = await _context.Pacientes.Where(p => p.Ativo).SumAsync(p => p.PsicoPontos);
            
            // Consultas gratuitas usadas (tipo gratuita)
            ConsultasGratuitasUsadas = await _context.Consultas
                .CountAsync(c => c.Tipo == TipoConsulta.Gratuita && c.Status == StatusConsulta.Realizada);
            
            // Consultas gratuitas disponíveis (10 pontos = 1 consulta)
            ConsultasGratuitasDisponiveis = TotalPontosDistribuidos / 10;
        }

        private async Task CarregarPacientesComPontosAsync()
        {
            var query = _context.Pacientes
                .Where(p => p.Ativo)
                .AsQueryable();

            // Filtros
            if (!string.IsNullOrWhiteSpace(BuscaNome))
            {
                query = query.Where(p => p.Nome.Contains(BuscaNome) || p.Email.Contains(BuscaNome));
            }

            if (FiltroMinPontos.HasValue)
            {
                query = query.Where(p => p.PsicoPontos >= FiltroMinPontos.Value);
            }

            // Ordenação
            switch (OrdenarPor?.ToLower())
            {
                case "nome":
                    query = Direcao == "asc" ? query.OrderBy(p => p.Nome) : query.OrderByDescending(p => p.Nome);
                    break;
                case "consultas":
                    query = Direcao == "asc" ? query.OrderBy(p => p.ConsultasRealizadas) : query.OrderByDescending(p => p.ConsultasRealizadas);
                    break;
                case "gratuitas":
                    query = Direcao == "asc" ? query.OrderBy(p => p.ConsultasGratuitas) : query.OrderByDescending(p => p.ConsultasGratuitas);
                    break;
                default: // pontos
                    query = Direcao == "asc" ? query.OrderBy(p => p.PsicoPontos) : query.OrderByDescending(p => p.PsicoPontos);
                    break;
            }

            var pacientes = await query.ToListAsync();

            PacientesLista = pacientes.Select(p => new PacienteComPontos
            {
                Id = p.Id,
                Nome = p.Nome,
                Email = p.Email,
                PsicoPontos = p.PsicoPontos,
                ConsultasRealizadas = p.ConsultasRealizadas,
                ConsultasGratuitas = p.ConsultasGratuitas,
                ConsultasGratuitasDisponiveis = p.PsicoPontos / 10,
                DataCadastro = p.DataCadastro
            }).ToList();
        }

        private async Task CarregarHistoricoRecenteAsync()
        {
            HistoricoRecente = await _context.HistoricoPontos
                .Include(h => h.Paciente)
                .OrderByDescending(h => h.DataMovimentacao)
                .Take(10)
                .ToListAsync();
        }
    }

    public class PacienteComPontos
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int PsicoPontos { get; set; }
        public int ConsultasRealizadas { get; set; }
        public int ConsultasGratuitas { get; set; }
        public int ConsultasGratuitasDisponiveis { get; set; }
        public DateTime DataCadastro { get; set; }
    }
}