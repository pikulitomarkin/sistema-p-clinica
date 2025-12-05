using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using ClinicaPsi.Web.Extensions;
using System.Security.Claims;

namespace ClinicaPsi.Web.Pages.Psicologo
{
    [Authorize(Roles = "Admin,Psicologo")]
    public class ConsultasModel : PageModel
    {
        private readonly AppDbContext _context;

        public ConsultasModel(AppDbContext context)
        {
            _context = context;
        }

        public List<Consulta> Consultas { get; set; } = new();
        public List<Paciente> PacientesDisponiveis { get; set; } = new();
        
        // Filtros
        public string? FiltroNomePaciente { get; set; }
        public StatusConsulta? FiltroStatus { get; set; }
        public TipoConsulta? FiltroTipo { get; set; }
        public DateTime? FiltroDataInicio { get; set; }
        public DateTime? FiltroDataFim { get; set; }
        public string Visualizacao { get; set; } = "lista";
        
        // Paginação
        public int PaginaAtual { get; set; } = 1;
        public int TotalConsultas { get; set; }
        public int TotalPaginas { get; set; }
        public int ItensPorPagina { get; set; } = 20;

        public async Task<IActionResult> OnGetAsync(
            string? paciente = null,
            StatusConsulta? status = null,
            TipoConsulta? tipo = null,
            DateTime? dataInicio = null,
            DateTime? dataFim = null,
            string visualizacao = "lista",
            int pagina = 1)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                    return Forbid();

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                
                int psicologoId;
                
                // Se for Admin, permitir acesso mas usar o primeiro psicólogo disponível
                if (User.IsInRole("Admin") && user?.PsicologoId == null)
                {
                    var primeiroPsicologo = await _context.Psicologos.FirstOrDefaultAsync();
                    if (primeiroPsicologo == null)
                        return NotFound("Nenhum psicólogo encontrado no sistema");
                    
                    psicologoId = primeiroPsicologo.Id;
                }
                else if (user?.PsicologoId == null)
                {
                    return Forbid();
                }
                else
                {
                    psicologoId = user.PsicologoId.Value;
                }

            // Definir filtros
            FiltroNomePaciente = paciente;
            FiltroStatus = status;
            FiltroTipo = tipo;
            FiltroDataInicio = dataInicio;
            FiltroDataFim = dataFim;
            Visualizacao = visualizacao;
            PaginaAtual = pagina;

            // Construir query base
            var query = _context.Consultas
                .Include(c => c.Paciente)
                .Where(c => c.PsicologoId == psicologoId);

            // Aplicar filtros
            if (!string.IsNullOrEmpty(FiltroNomePaciente))
            {
                query = query.Where(c => c.Paciente!.Nome.Contains(FiltroNomePaciente));
            }

            if (FiltroStatus.HasValue)
            {
                query = query.Where(c => c.Status == FiltroStatus.Value);
            }

            if (FiltroTipo.HasValue)
            {
                query = query.Where(c => c.Tipo == FiltroTipo.Value);
            }

            if (FiltroDataInicio.HasValue)
            {
                query = query.Where(c => c.DataHorario.Date >= FiltroDataInicio.Value.Date);
            }

            if (FiltroDataFim.HasValue)
            {
                query = query.Where(c => c.DataHorario.Date <= FiltroDataFim.Value.Date);
            }

            // Calcular totais
            TotalConsultas = await query.CountAsync();
            TotalPaginas = (int)Math.Ceiling((double)TotalConsultas / ItensPorPagina);

            // Aplicar paginação e ordenação
            Consultas = await query
                .OrderByDescending(c => c.DataHorario)
                .Skip((PaginaAtual - 1) * ItensPorPagina)
                .Take(ItensPorPagina)
                .ToListAsync();

                // Buscar pacientes disponíveis
                PacientesDisponiveis = await _context.Pacientes
                    .Where(p => _context.Consultas
                        .Any(c => c.PacienteId == p.Id && c.PsicologoId == psicologoId))
                    .Union(_context.Pacientes.Take(20))
                    .Distinct()
                    .OrderBy(p => p.Nome)
                    .ToListAsync();

                return Page();
            }
            catch (Exception ex)
            {
                TempData["Error"] = "A página está sendo atualizada. Por favor, aguarde alguns minutos e recarregue.";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostNovaConsultaAsync(
            int pacienteId,
            string tipoConsulta,
            DateTime dataConsulta,
            string horaConsulta,
            int duracao,
            decimal valor,
            string? observacoes)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Forbid();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user?.PsicologoId == null)
                return Forbid();

            try
            {
                // Combinar data e hora
                if (!TimeSpan.TryParse(horaConsulta, out var hora))
                {
                    ModelState.AddModelError("", "Horário inválido");
                    return Page();
                }

                var dataHorario = dataConsulta.Date.Add(hora);

                // Verificar conflitos
                var consultaExistente = await _context.Consultas
                    .AnyAsync(c => c.PsicologoId == user.PsicologoId &&
                                  c.DataHorario == dataHorario &&
                                  c.Status != StatusConsulta.Cancelada);

                if (consultaExistente)
                {
                    ModelState.AddModelError("", "Já existe uma consulta agendada para este horário");
                    return await OnGetAsync();
                }

                // Criar consulta
                var novaConsulta = new Consulta
                {
                    PacienteId = pacienteId,
                    PsicologoId = user.PsicologoId.Value,
                    DataHorario = dataHorario,
                    DuracaoMinutos = duracao,
                    Valor = valor,
                    Status = StatusConsulta.Agendada,
                    Tipo = Enum.Parse<TipoConsulta>(tipoConsulta),
                    Observacoes = observacoes,
                    DataCriacao = DateTime.Now
                };

                _context.Consultas.Add(novaConsulta);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Consulta criada com sucesso!";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Erro ao criar consulta: " + ex.Message);
                return await OnGetAsync();
            }
        }

        public async Task<IActionResult> OnPostCancelarConsultaAsync(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Forbid();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user?.PsicologoId == null)
                return Forbid();

            try
            {
                var consulta = await _context.Consultas
                    .FirstOrDefaultAsync(c => c.Id == id && c.PsicologoId == user.PsicologoId);

                if (consulta == null)
                    return NotFound();

                consulta.Status = StatusConsulta.Cancelada;
                consulta.DataAtualizacao = DateTime.Now;

                await _context.SaveChangesAsync();

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, error = ex.Message });
            }
        }
    }
}