using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ClinicaPsi.Application.Services;
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
        private readonly UsuarioPacienteOnboardingService _onboardingService;
        private readonly ILogger<PacientesModel> _logger;

        public PacientesModel(
            AppDbContext context,
            UsuarioPacienteOnboardingService onboardingService,
            ILogger<PacientesModel> logger)
        {
            _context = context;
            _onboardingService = onboardingService;
            _logger = logger;
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

            // Buscar todos os pacientes ativos
            var pacientesQuery = _context.Pacientes
                .Where(p => p.Ativo);

            // Aplicar filtro de busca
            if (!string.IsNullOrEmpty(FiltroBusca))
            {
                pacientesQuery = pacientesQuery.Where(p => 
                    p.Nome.Contains(FiltroBusca) ||
                    p.Email.Contains(FiltroBusca) ||
                    (p.CPF != null && p.CPF.Contains(FiltroBusca.Replace(".", "").Replace("-", ""))));
            }

            // Aplicar filtro de período (pacientes que tiveram consulta no período)
            if (FiltroPeriodo.HasValue)
            {
                var dataLimite = DateTime.Now.AddDays(-FiltroPeriodo.Value);
                pacientesQuery = pacientesQuery.Where(p => 
                    _context.Consultas.Any(c => 
                        c.PacienteId == p.Id && 
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
            {
                TempData["Error"] = "Usuário não autenticado";
                return RedirectToPage();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user?.PsicologoId == null)
            {
                TempData["Error"] = "Psicólogo não encontrado";
                return RedirectToPage();
            }

            // Validar dados obrigatórios
            if (string.IsNullOrWhiteSpace(nome))
            {
                TempData["Error"] = "Nome é obrigatório";
                return RedirectToPage();
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["Error"] = "Email é obrigatório";
                return RedirectToPage();
            }

            // CPF, Telefone e Data de Nascimento são obrigatórios na entidade
            if (string.IsNullOrWhiteSpace(cpf))
            {
                TempData["Error"] = "CPF é obrigatório";
                return RedirectToPage();
            }

            if (string.IsNullOrWhiteSpace(telefone))
            {
                TempData["Error"] = "Telefone é obrigatório";
                return RedirectToPage();
            }

            if (!dataNascimento.HasValue)
            {
                TempData["Error"] = "Data de nascimento é obrigatória";
                return RedirectToPage();
            }

            try
            {
                // Limpar e validar email
                email = email.Trim().ToLower();
                
                // Verificar se email já existe
                var emailExiste = await _context.Pacientes.AnyAsync(p => p.Email == email);
                if (emailExiste)
                {
                    TempData["Error"] = "Já existe um paciente cadastrado com este email";
                    return RedirectToPage();
                }

                // Limpar CPF
                cpf = cpf.Replace(".", "").Replace("-", "").Replace(" ", "").Trim();
                
                // Validar CPF (deve ter 11 dígitos)
                if (cpf.Length != 11 || !cpf.All(char.IsDigit))
                {
                    TempData["Error"] = "CPF inválido. Deve conter 11 dígitos";
                    return RedirectToPage();
                }
                
                // Verificar se CPF já existe
                var cpfExiste = await _context.Pacientes.AnyAsync(p => p.CPF == cpf);
                if (cpfExiste)
                {
                    TempData["Error"] = "Já existe um paciente cadastrado com este CPF";
                    return RedirectToPage();
                }

                // Limpar telefone
                telefone = telefone.Replace("(", "").Replace(")", "").Replace("-", "").Replace(" ", "").Trim();

                // Criar novo paciente
                var novoPaciente = new Paciente
                {
                    Nome = nome.Trim(),
                    Email = email,
                    CPF = cpf,
                    Telefone = telefone,
                    DataNascimento = dataNascimento.Value,
                    ContatoEmergencia = string.IsNullOrWhiteSpace(contatoEmergencia) ? null : contatoEmergencia.Trim(),
                    Endereco = string.IsNullOrWhiteSpace(endereco) ? null : endereco.Trim(),
                    HistoricoMedico = string.IsNullOrWhiteSpace(historicoMedico) ? null : historicoMedico.Trim(),
                    PsicoPontos = 0,
                    ConsultasRealizadas = 0,
                    ConsultasGratuitas = 0,
                    DataCadastro = DateTime.Now,
                    DataCriacao = DateTime.Now,
                    Ativo = true
                };

                _context.Pacientes.Add(novoPaciente);
                var resultado = await _context.SaveChangesAsync();

                if (resultado <= 0)
                {
                    TempData["Error"] = "Não foi possível salvar o paciente. Tente novamente.";
                    return RedirectToPage();
                }

                try
                {
                    var onboarding = await _onboardingService.GarantirAcessoClienteEEnviarBoasVindasAsync(novoPaciente);
                    if (onboarding.UsuarioCriadoOuAtualizado && onboarding.EmailEnviado)
                    {
                        TempData["Success"] = "Paciente cadastrado com sucesso! E-mail com login e senha provisória enviado.";
                    }
                    else if (onboarding.UsuarioCriadoOuAtualizado && !onboarding.EmailEnviado)
                    {
                        TempData["Success"] = "Paciente cadastrado com sucesso!";
                        TempData["Warning"] =
                            $"Não foi possível enviar o e-mail de acesso ({onboarding.EmailErro}). " +
                            $"Informe ao paciente — Login: {onboarding.LoginEmail} | Senha provisória: {onboarding.SenhaProvisoria}";
                        _logger.LogWarning("Cadastro paciente {Id}: e-mail falhou ({Erro})", novoPaciente.Id, onboarding.EmailErro);
                    }
                    else
                    {
                        TempData["Success"] = "Paciente cadastrado com sucesso!";
                        if (!string.IsNullOrWhiteSpace(onboarding.EmailErro))
                            TempData["Warning"] = $"Acesso ao portal não criado: {onboarding.EmailErro}";
                    }
                }
                catch (Exception onboardingEx)
                {
                    _logger.LogError(onboardingEx, "Erro no onboarding de e-mail do paciente {Id}", novoPaciente.Id);
                    TempData["Success"] = "Paciente cadastrado com sucesso!";
                    TempData["Warning"] = "Paciente salvo, mas houve falha ao criar acesso/enviar e-mail. Tente reenviar depois.";
                }

                return RedirectToPage();
            }
            catch (DbUpdateException dbEx)
            {
                TempData["Error"] = $"Erro ao salvar no banco de dados: {dbEx.InnerException?.Message ?? dbEx.Message}";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro ao cadastrar paciente: {ex.Message}";
                return RedirectToPage();
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
        }
    }
}