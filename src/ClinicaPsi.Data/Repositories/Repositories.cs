using Microsoft.EntityFrameworkCore;
using ClinicaPsi.Data.Context;
using ClinicaPsi.Shared.Models;
using ClinicaPsi.Core.Services;

namespace ClinicaPsi.Data.Repositories;

public class BaseRepository<T> : IBaseRepository<T> where T : class
{
    protected readonly ClinicaPsiDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public BaseRepository(ClinicaPsiDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(int id)
    {
        return await _dbSet.FindAsync(id);
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _dbSet.ToListAsync();
    }

    public virtual async Task<T> AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public virtual async Task<T> UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public virtual async Task DeleteAsync(int id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            _dbSet.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }

    public virtual async Task<bool> ExistsAsync(int id)
    {
        return await _dbSet.FindAsync(id) != null;
    }
}

    }
}

public class PacienteRepository : BaseRepository<Paciente>, IPacienteRepository

public interface IPsicologoRepository : IBaseRepository<Psicologo>
{
    Task<Psicologo?> GetByEmailAsync(string email);
    Task<Psicologo?> GetByCRPAsync(string crp);
    Task<IEnumerable<Psicologo>> GetAtivosAsync();
}

public class PsicologoRepository : BaseRepository<Psicologo>, IPsicologoRepository
{
    public PsicologoRepository(ClinicaPsiDbContext context) : base(context)
    {
    }

    public async Task<Psicologo?> GetByEmailAsync(string email)
    {
        return await _dbSet.FirstOrDefaultAsync(p => p.Email == email);
    }

    public async Task<Psicologo?> GetByCRPAsync(string crp)
    {
        return await _dbSet.FirstOrDefaultAsync(p => p.CRP == crp);
    }

    public async Task<IEnumerable<Psicologo>> GetAtivosAsync()
    {
        return await _dbSet.Where(p => p.Ativo).ToListAsync();
    }
}

public interface IConsultaRepository : IBaseRepository<Consulta>
{
    Task<IEnumerable<Consulta>> GetByPacienteAsync(int pacienteId);
    Task<IEnumerable<Consulta>> GetByPsicologoAsync(int psicologoId);
    Task<IEnumerable<Consulta>> GetByDataAsync(DateTime data);
    Task<IEnumerable<Consulta>> GetByPeriodoAsync(DateTime dataInicio, DateTime dataFim);
    Task<bool> ExisteConflitorHorarioAsync(int psicologoId, DateTime dataHorario, int? consultaId = null);
    Task<IEnumerable<Consulta>> GetProximasConsultasAsync(int dias = 7);
    Task<IEnumerable<Consulta>> GetConsultasComNotificacaoPendenteAsync();
}

public class ConsultaRepository : BaseRepository<Consulta>, IConsultaRepository
{
    public ConsultaRepository(ClinicaPsiDbContext context) : base(context)
    {
    }

    public override async Task<Consulta?> GetByIdAsync(int id)
    {
        return await _dbSet
            .Include(c => c.Paciente)
            .Include(c => c.Psicologo)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<IEnumerable<Consulta>> GetByPacienteAsync(int pacienteId)
    {
        return await _dbSet
            .Include(c => c.Psicologo)
            .Where(c => c.PacienteId == pacienteId)
            .OrderByDescending(c => c.DataHorario)
            .ToListAsync();
    }

    public async Task<IEnumerable<Consulta>> GetByPsicologoAsync(int psicologoId)
    {
        return await _dbSet
            .Include(c => c.Paciente)
            .Where(c => c.PsicologoId == psicologoId)
            .OrderByDescending(c => c.DataHorario)
            .ToListAsync();
    }

    public async Task<IEnumerable<Consulta>> GetByDataAsync(DateTime data)
    {
        var dataInicio = data.Date;
        var dataFim = dataInicio.AddDays(1);

        return await _dbSet
            .Include(c => c.Paciente)
            .Include(c => c.Psicologo)
            .Where(c => c.DataHorario >= dataInicio && c.DataHorario < dataFim)
            .OrderBy(c => c.DataHorario)
            .ToListAsync();
    }

    public async Task<IEnumerable<Consulta>> GetByPeriodoAsync(DateTime dataInicio, DateTime dataFim)
    {
        return await _dbSet
            .Include(c => c.Paciente)
            .Include(c => c.Psicologo)
            .Where(c => c.DataHorario >= dataInicio && c.DataHorario <= dataFim)
            .OrderBy(c => c.DataHorario)
            .ToListAsync();
    }

    public async Task<bool> ExisteConflitorHorarioAsync(int psicologoId, DateTime dataHorario, int? consultaId = null)
    {
        var query = _dbSet.Where(c => 
            c.PsicologoId == psicologoId &&
            c.Status != StatusConsulta.Cancelada &&
            c.DataHorario <= dataHorario.AddMinutes(50) &&
            c.DataHorario.AddMinutes(c.DuracaoMinutos) > dataHorario);

        if (consultaId.HasValue)
        {
            query = query.Where(c => c.Id != consultaId.Value);
        }

        return await query.AnyAsync();
    }

    public async Task<IEnumerable<Consulta>> GetProximasConsultasAsync(int dias = 7)
    {
        var dataLimite = DateTime.Now.AddDays(dias);

        return await _dbSet
            .Include(c => c.Paciente)
            .Include(c => c.Psicologo)
            .Where(c => c.DataHorario >= DateTime.Now && 
                       c.DataHorario <= dataLimite &&
                       c.Status == StatusConsulta.Agendada)
            .OrderBy(c => c.DataHorario)
            .ToListAsync();
    }

    public async Task<IEnumerable<Consulta>> GetConsultasComNotificacaoPendenteAsync()
    {
        var amanha = DateTime.Now.AddDays(1);
        var dataLimite = amanha.AddDays(1);

        return await _dbSet
            .Include(c => c.Paciente)
            .Include(c => c.Psicologo)
            .Where(c => c.DataHorario >= amanha && 
                       c.DataHorario < dataLimite &&
                       c.Status == StatusConsulta.Agendada &&
                       !c.NotificacaoEnviada)
            .ToListAsync();
    }
}

public interface IHistoricoPontosRepository : IBaseRepository<HistoricoPontos>
{
    Task<IEnumerable<HistoricoPontos>> GetByPacienteAsync(int pacienteId);
    Task<int> GetTotalPontosPacienteAsync(int pacienteId);
}

public class HistoricoPontosRepository : BaseRepository<HistoricoPontos>, IHistoricoPontosRepository
{
    public HistoricoPontosRepository(ClinicaPsiDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<HistoricoPontos>> GetByPacienteAsync(int pacienteId)
    {
        return await _dbSet
            .Include(h => h.Consulta)
            .Where(h => h.PacienteId == pacienteId)
            .OrderByDescending(h => h.DataMovimentacao)
            .ToListAsync();
    }

    public async Task<int> GetTotalPontosPacienteAsync(int pacienteId)
    {
        return await _dbSet
            .Where(h => h.PacienteId == pacienteId)
            .SumAsync(h => h.PontosAlterados);
    }
}