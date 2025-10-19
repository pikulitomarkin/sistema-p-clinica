using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace ClinicaPsi.Application.Services;

public class ConsultaService
{
    private readonly AppDbContext _context;

    public ConsultaService(AppDbContext context) => _context = context;

    public async Task<List<Consulta>> GetAllAsync()
    {
        return await _context.Consultas
            .Include(c => c.Paciente)
            .Include(c => c.Psicologo)
            .OrderByDescending(c => c.DataHorario)
            .ToListAsync();
    }

    public async Task<List<Consulta>> GetProximasConsultasAsync(int dias = 7)
    {
        var dataFim = DateTime.UtcNow.AddDays(dias);
        return await _context.Consultas
            .Include(c => c.Paciente)
            .Include(c => c.Psicologo)
            .Where(c => c.DataHorario <= dataFim && c.Status == StatusConsulta.Agendada)
            .OrderBy(c => c.DataHorario)
            .ToListAsync();
    }

    public async Task<Consulta> AgendarAsync(Consulta consulta)
    {
        var psicologo = await _context.Psicologos.FindAsync(consulta.PsicologoId);
        var paciente = await _context.Pacientes.FindAsync(consulta.PacienteId);

        if (psicologo == null || paciente == null)
            throw new Exception("Psicólogo ou paciente não encontrado");

        // Verificar se é consulta gratuita
        if (paciente.PsicoPontos >= 10 && consulta.Tipo == TipoConsulta.Gratuita)
        {
            consulta.Valor = 0;
            paciente.PsicoPontos -= 10;
            paciente.ConsultasGratuitas++;
        }
        else
        {
            consulta.Valor = psicologo.ValorConsulta;
            consulta.Tipo = TipoConsulta.Normal;
        }

        consulta.DataAgendamento = DateTime.UtcNow;
        consulta.Status = StatusConsulta.Agendada;

        _context.Consultas.Add(consulta);
        await _context.SaveChangesAsync();

        return consulta;
    }

    public async Task<bool> CancelarAsync(int id, string motivo)
    {
        var consulta = await _context.Consultas.FindAsync(id);
        if (consulta == null) return false;

        consulta.Status = StatusConsulta.Cancelada;
        consulta.DataCancelamento = DateTime.UtcNow;
        consulta.MotivoCancelamento = motivo;

        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> RealizarAsync(int id, string? relatorio = null)
    {
        var consulta = await _context.Consultas
            .Include(c => c.Paciente)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (consulta == null) return false;

        consulta.Status = StatusConsulta.Realizada;
        consulta.RelatorioSessao = relatorio;

        // Adicionar pontos ao paciente
        if (consulta.Tipo != TipoConsulta.Gratuita)
        {
            consulta.Paciente.PsicoPontos++;
            consulta.Paciente.ConsultasRealizadas++;

            var historico = new HistoricoPontos
            {
                PacienteId = consulta.PacienteId,
                ConsultaId = consulta.Id,
                PontosAlterados = 1,
                Motivo = "Consulta realizada",
                DataMovimentacao = DateTime.UtcNow
            };

            _context.HistoricoPontos.Add(historico);
        }

        return await _context.SaveChangesAsync() > 0;
    }
}
