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
    public class PacientesModel : PageModel
    {
        private readonly AppDbContext _context;

        public PacientesModel(AppDbContext context)
        {
            _context = context;
        }

        public List<Paciente> Pacientes { get; set; } = new();
        public List<Consulta> UltimasConsultas { get; set; } = new();
        public List<Consulta> ProximasConsultas { get; set; } = new();
        public Dictionary<int, int> TotalConsultasPorPaciente { get; set; } = new();
        
        // Filtros
        public string? FiltroBusca { get; set; }
        public string Ordenacao { get; set; } = "nome";
        public int? FiltroPeriodo { get; set; }
        public string Visualizacao { get; set; } = "cards";
        
        // Paginação
        public int PaginaAtual { get; set; } = 1;
        public int TotalPacientes { get; set; }
        public int TotalPaginas { get; set; }
        public int ItensPorPagina { get; set; } = 12;
        
        // Estatísticas
        public int PacientesAtivos30Dias { get; set; }
        public int PacientesInativos90Dias { get; set; }
        public double MediaPsicoPontos { get; set; }

        public async Task<IActionResult> OnGetAsync(
            string? busca = null,
            string ordenacao = "nome",
            int? periodo = null,
            string visualizacao = "cards",
            int pagina = 1)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                    return Forbid();

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user?.PsicologoId == null)
                    return Forbid();

                var psicologoId = user.PsicologoId.Value;

            // Definir filtros
            FiltroBusca = busca;
            Ordenacao = ordenacao;
            FiltroPeriodo = periodo;
            Visualizacao = visualizacao;
            PaginaAtual = pagina;

            // Buscar pacientes que já tiveram consulta com este psicólogo
            var pacientesQuery = _context.Pacientes
                .Where(p => _context.Consultas
                    .Any(c => c.PacienteId == p.Id && c.PsicologoId == psicologoId));

            // Aplicar filtro de busca
            if (!string.IsNullOrEmpty(FiltroBusca))
            {
                pacientesQuery = pacientesQuery.Where(p => 
                    p.Nome.Contains(FiltroBusca) ||
                    p.Email.Contains(FiltroBusca) ||
                    (p.CPF != null && p.CPF.Contains(FiltroBusca.Replace(".", "").Replace("-", ""))));
            }

            // Aplicar filtro de período
            if (FiltroPeriodo.HasValue)
            {
                var dataLimite = DateTime.Now.AddDays(-FiltroPeriodo.Value);
                pacientesQuery = pacientesQuery.Where(p => 
                    _context.Consultas.Any(c => 
                        c.PacienteId == p.Id && 
                        c.PsicologoId == psicologoId && 
                        c.DataHorario >= dataLimite));
            }

            // Aplicar ordenação
            pacientesQuery = Ordenacao switch
            {
                "ultimaConsulta" => pacientesQuery.OrderByDescending(p => 
                    _context.Consultas
                        .Where(c => c.PacienteId == p.Id && c.PsicologoId == psicologoId)
                        .Max(c => c.DataHorario)),
                "totalConsultas" => pacientesQuery.OrderByDescending(p => 
                    _context.Consultas
                        .Count(c => c.PacienteId == p.Id && c.PsicologoId == psicologoId)),
                "pontos" => pacientesQuery.OrderByDescending(p => p.PsicoPontos),
                _ => pacientesQuery.OrderBy(p => p.Nome)
            };

            // Calcular totais
            TotalPacientes = await pacientesQuery.CountAsync();
            TotalPaginas = (int)Math.Ceiling((double)TotalPacientes / ItensPorPagina);

            // Aplicar paginação
            Pacientes = await pacientesQuery
                .Skip((PaginaAtual - 1) * ItensPorPagina)
                .Take(ItensPorPagina)
                .ToListAsync();

            // Buscar dados adicionais
            var pacienteIds = Pacientes.Select(p => p.Id).ToList();

            // Últimas consultas
            UltimasConsultas = await _context.Consultas
                .Where(c => pacienteIds.Contains(c.PacienteId) && c.PsicologoId == psicologoId)
                .GroupBy(c => c.PacienteId)
                .Select(g => g.OrderByDescending(c => c.DataHorario).First())
                .ToListAsync();

            // Próximas consultas
            ProximasConsultas = await _context.Consultas
                .Where(c => pacienteIds.Contains(c.PacienteId) && 
                           c.PsicologoId == psicologoId &&
                           c.DataHorario > DateTime.Now &&
                           c.Status != StatusConsulta.Cancelada)
                .GroupBy(c => c.PacienteId)
                .Select(g => g.OrderBy(c => c.DataHorario).First())
                .ToListAsync();

            // Total de consultas por paciente
            TotalConsultasPorPaciente = await _context.Consultas
                .Where(c => pacienteIds.Contains(c.PacienteId) && c.PsicologoId == psicologoId)
                .GroupBy(c => c.PacienteId)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

                // Calcular estatísticas
                await CalcularEstatisticasAsync(psicologoId);

                return Page();
            }
            catch (Exception ex)
            {
                TempData["Error"] = "A página está sendo atualizada. Por favor, aguarde alguns minutos e recarregue.";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostNovoPacienteAsync(
            string nome,
            string email,
            string? cpf,
            string? telefone,
            DateTime? dataNascimento,
            string? contatoEmergencia,
            string? endereco,
            string? historicoMedico)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Forbid();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user?.PsicologoId == null)
                return Forbid();

            try
            {
                // Verificar se email já existe
                var emailExiste = await _context.Pacientes.AnyAsync(p => p.Email == email);
                if (emailExiste)
                {
                    ModelState.AddModelError("", "Já existe um paciente cadastrado com este email");
                    return await OnGetAsync();
                }

                // Limpar CPF
                if (!string.IsNullOrEmpty(cpf))
                {
                    cpf = cpf.Replace(".", "").Replace("-", "");
                    
                    // Verificar se CPF já existe
                    var cpfExiste = await _context.Pacientes.AnyAsync(p => p.CPF == cpf);
                    if (cpfExiste)
                    {
                        ModelState.AddModelError("", "Já existe um paciente cadastrado com este CPF");
                        return await OnGetAsync();
                    }
                }

                // Criar novo paciente
                var novoPaciente = new Paciente
                {
                    Nome = nome,
                    Email = email,
                    CPF = cpf,
                    Telefone = telefone,
                    DataNascimento = dataNascimento ?? DateTime.Now,
                    ContatoEmergencia = contatoEmergencia,
                    Endereco = endereco,
                    HistoricoMedico = historicoMedico,
                    PsicoPontos = 0,
                    ConsultasRealizadas = 0,
                    ConsultasGratuitas = 0,
                    DataCadastro = DateTime.Now
                };

                _context.Pacientes.Add(novoPaciente);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Paciente cadastrado com sucesso!";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Erro ao cadastrar paciente: " + ex.Message);
                return await OnGetAsync();
            }
        }

        private async Task CalcularEstatisticasAsync(int psicologoId)
        {
            var dataAtual = DateTime.Now;
            var data30DiasAtras = dataAtual.AddDays(-30);
            var data90DiasAtras = dataAtual.AddDays(-90);

            // Pacientes ativos nos últimos 30 dias
            PacientesAtivos30Dias = await _context.Consultas
                .Where(c => c.PsicologoId == psicologoId && c.DataHorario >= data30DiasAtras)
                .Select(c => c.PacienteId)
                .Distinct()
                .CountAsync();

            // Pacientes inativos há mais de 90 dias
            var pacientesComConsulta = await _context.Consultas
                .Where(c => c.PsicologoId == psicologoId)
                .GroupBy(c => c.PacienteId)
                .Where(g => g.Max(c => c.DataHorario) < data90DiasAtras)
                .CountAsync();

            PacientesInativos90Dias = pacientesComConsulta;

            // Média de PsicoPontos
            var pacientesComPontos = await _context.Pacientes
                .Where(p => _context.Consultas
                    .Any(c => c.PacienteId == p.Id && c.PsicologoId == psicologoId))
                .Select(p => p.PsicoPontos)
                .ToListAsync();

            MediaPsicoPontos = pacientesComPontos.Any() ? pacientesComPontos.Average() : 0;
        }
    }
}