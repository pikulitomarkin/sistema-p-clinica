using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClinicaPsi.Application.Services;

/// <summary>
/// Serviço para gerenciar prontuários eletrônicos
/// </summary>
public class ProntuarioService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ProntuarioService> _logger;

    public ProntuarioService(AppDbContext context, ILogger<ProntuarioService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Cria um novo prontuário
    /// </summary>
    public async Task<ProntuarioEletronico> CriarProntuarioAsync(ProntuarioEletronico prontuario)
    {
        _context.ProntuariosEletronicos.Add(prontuario);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation($"Prontuário criado para paciente ID {prontuario.PacienteId}");
        return prontuario;
    }

    /// <summary>
    /// Atualiza um prontuário existente
    /// </summary>
    public async Task<ProntuarioEletronico> AtualizarProntuarioAsync(ProntuarioEletronico prontuario)
    {
        prontuario.DataAtualizacao = DateTime.Now;
        _context.ProntuariosEletronicos.Update(prontuario);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation($"Prontuário ID {prontuario.Id} atualizado");
        return prontuario;
    }

    /// <summary>
    /// Obtém prontuário por ID
    /// </summary>
    public async Task<ProntuarioEletronico?> ObterPorIdAsync(int id)
    {
        return await _context.ProntuariosEletronicos
            .Include(p => p.Paciente)
            .Include(p => p.Psicologo)
            .Include(p => p.Consulta)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    /// <summary>
    /// Obtém todos os prontuários de um paciente
    /// </summary>
    public async Task<List<ProntuarioEletronico>> ObterPorPacienteAsync(int pacienteId)
    {
        return await _context.ProntuariosEletronicos
            .Include(p => p.Psicologo)
            .Include(p => p.Consulta)
            .Where(p => p.PacienteId == pacienteId)
            .OrderByDescending(p => p.DataSessao)
            .ToListAsync();
    }

    /// <summary>
    /// Obtém prontuários por psicólogo
    /// </summary>
    public async Task<List<ProntuarioEletronico>> ObterPorPsicologoAsync(int psicologoId, DateTime? dataInicio = null, DateTime? dataFim = null)
    {
        var query = _context.ProntuariosEletronicos
            .Include(p => p.Paciente)
            .Include(p => p.Consulta)
            .Where(p => p.PsicologoId == psicologoId);

        if (dataInicio.HasValue)
            query = query.Where(p => p.DataSessao >= dataInicio.Value);

        if (dataFim.HasValue)
            query = query.Where(p => p.DataSessao <= dataFim.Value);

        return await query
            .OrderByDescending(p => p.DataSessao)
            .ToListAsync();
    }

    /// <summary>
    /// Obtém prontuário por consulta
    /// </summary>
    public async Task<ProntuarioEletronico?> ObterPorConsultaAsync(int consultaId)
    {
        return await _context.ProntuariosEletronicos
            .Include(p => p.Paciente)
            .Include(p => p.Psicologo)
            .FirstOrDefaultAsync(p => p.ConsultaId == consultaId);
    }

    /// <summary>
    /// Finaliza um prontuário (impede edições futuras)
    /// </summary>
    public async Task<bool> FinalizarProntuarioAsync(int id)
    {
        var prontuario = await ObterPorIdAsync(id);
        if (prontuario == null) return false;

        prontuario.Finalizado = true;
        prontuario.DataAtualizacao = DateTime.Now;
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Prontuário ID {id} finalizado");
        return true;
    }

    /// <summary>
    /// Exclui um prontuário (apenas se não finalizado)
    /// </summary>
    public async Task<bool> ExcluirProntuarioAsync(int id)
    {
        var prontuario = await ObterPorIdAsync(id);
        if (prontuario == null || prontuario.Finalizado) return false;

        _context.ProntuariosEletronicos.Remove(prontuario);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Prontuário ID {id} excluído");
        return true;
    }

    /// <summary>
    /// Busca prontuários por texto
    /// </summary>
    public async Task<List<ProntuarioEletronico>> BuscarProntuariosAsync(string termo, int? pacienteId = null, int? psicologoId = null)
    {
        var query = _context.ProntuariosEletronicos
            .Include(p => p.Paciente)
            .Include(p => p.Psicologo)
            .AsQueryable();

        if (pacienteId.HasValue)
            query = query.Where(p => p.PacienteId == pacienteId.Value);

        if (psicologoId.HasValue)
            query = query.Where(p => p.PsicologoId == psicologoId.Value);

        if (!string.IsNullOrWhiteSpace(termo))
        {
            termo = termo.ToLower();
            query = query.Where(p => 
                p.QueixaPrincipal.ToLower().Contains(termo) ||
                p.Observacoes.ToLower().Contains(termo) ||
                (p.Evolucao != null && p.Evolucao.ToLower().Contains(termo)) ||
                (p.Intervencoes != null && p.Intervencoes.ToLower().Contains(termo)));
        }

        return await query
            .OrderByDescending(p => p.DataSessao)
            .Take(50)
            .ToListAsync();
    }

    /// <summary>
    /// Obtém estatísticas de atendimentos
    /// </summary>
    public async Task<EstatisticasProntuario> ObterEstatisticasAsync(int? pacienteId = null, int? psicologoId = null)
    {
        var query = _context.ProntuariosEletronicos.AsQueryable();

        if (pacienteId.HasValue)
            query = query.Where(p => p.PacienteId == pacienteId.Value);

        if (psicologoId.HasValue)
            query = query.Where(p => p.PsicologoId == psicologoId.Value);

        var prontuarios = await query.ToListAsync();

        return new EstatisticasProntuario
        {
            TotalSessoes = prontuarios.Count,
            SessoesFinalizadas = prontuarios.Count(p => p.Finalizado),
            SessoesEmAndamento = prontuarios.Count(p => !p.Finalizado),
            PrimeiraSessao = prontuarios.Any() ? prontuarios.Min(p => p.DataSessao) : null,
            UltimaSessao = prontuarios.Any() ? prontuarios.Max(p => p.DataSessao) : null,
            MediaSessoesPorMes = CalcularMediaSessoesPorMes(prontuarios)
        };
    }

    private decimal CalcularMediaSessoesPorMes(List<ProntuarioEletronico> prontuarios)
    {
        if (!prontuarios.Any()) return 0;

        var primeiraData = prontuarios.Min(p => p.DataSessao);
        var ultimaData = prontuarios.Max(p => p.DataSessao);
        
        var meses = ((ultimaData.Year - primeiraData.Year) * 12) + ultimaData.Month - primeiraData.Month + 1;
        
        return meses > 0 ? (decimal)prontuarios.Count / meses : prontuarios.Count;
    }
}

/// <summary>
/// Estatísticas de prontuários
/// </summary>
public class EstatisticasProntuario
{
    public int TotalSessoes { get; set; }
    public int SessoesFinalizadas { get; set; }
    public int SessoesEmAndamento { get; set; }
    public DateTime? PrimeiraSessao { get; set; }
    public DateTime? UltimaSessao { get; set; }
    public decimal MediaSessoesPorMes { get; set; }
}
