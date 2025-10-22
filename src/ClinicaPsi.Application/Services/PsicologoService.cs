using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace ClinicaPsi.Application.Services;

public class PsicologoService
{
    private readonly AppDbContext _context;

    public PsicologoService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Psicologo>> GetAllAsync()
    {
        return await _context.Psicologos
            .Where(p => p.Ativo)
            .OrderBy(p => p.Nome)
            .ToListAsync();
    }

    public async Task<Psicologo?> GetByIdAsync(int id)
    {
        return await _context.Psicologos.FindAsync(id);
    }

    public async Task<List<DateTime>> GetHorariosDisponiveisAsync(int psicologoId, DateTime data)
    {
        var psicologo = await GetByIdAsync(psicologoId);
        if (psicologo == null) return new List<DateTime>();

        var diaSemana = data.DayOfWeek;
        var atende = diaSemana switch
        {
            DayOfWeek.Monday => psicologo.AtendeSegunda,
            DayOfWeek.Tuesday => psicologo.AtendeTerca,
            DayOfWeek.Wednesday => psicologo.AtendeQuarta,
            DayOfWeek.Thursday => psicologo.AtendeQuinta,
            DayOfWeek.Friday => psicologo.AtendeSexta,
            DayOfWeek.Saturday => psicologo.AtendeSabado,
            DayOfWeek.Sunday => psicologo.AtendeDomingo,
            _ => false
        };

        if (!atende) return new List<DateTime>();

        var consultasAgendadas = await _context.Consultas
            .Where(c => c.PsicologoId == psicologoId 
                     && c.DataHorario.Date == data.Date
                     && c.Status != StatusConsulta.Cancelada)
            .Select(c => c.DataHorario)
            .ToListAsync();

        var horarios = new List<DateTime>();
        var horarioAtual = data.Date.Add(psicologo.HorarioInicioManha);
        var fimManha = data.Date.Add(psicologo.HorarioFimManha);

        // Horários da manhã
        while (horarioAtual < fimManha)
        {
            if (!consultasAgendadas.Contains(horarioAtual))
                horarios.Add(horarioAtual);
            horarioAtual = horarioAtual.AddMinutes(50);
        }

        // Horários da tarde
        horarioAtual = data.Date.Add(psicologo.HorarioInicioTarde);
        var fimTarde = data.Date.Add(psicologo.HorarioFimTarde);

        while (horarioAtual < fimTarde)
        {
            if (!consultasAgendadas.Contains(horarioAtual))
                horarios.Add(horarioAtual);
            horarioAtual = horarioAtual.AddMinutes(50);
        }

        return horarios;
    }

    public async Task<Psicologo> CreateAsync(Psicologo psicologo)
    {
        _context.Psicologos.Add(psicologo);
        await _context.SaveChangesAsync();
        return psicologo;
    }

    public async Task<Psicologo> UpdateAsync(Psicologo psicologo)
    {
        _context.Entry(psicologo).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        return psicologo;
    }

    public async Task DeleteAsync(int id)
    {
        var psicologo = await GetByIdAsync(id);
        if (psicologo != null)
        {
            psicologo.Ativo = false;
            await UpdateAsync(psicologo);
        }
    }
}
