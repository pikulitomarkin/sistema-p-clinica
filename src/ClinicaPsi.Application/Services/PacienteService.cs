using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace ClinicaPsi.Application.Services;

public class PacienteService
{
    private readonly AppDbContext _context;

    public PacienteService(AppDbContext context) => _context = context;

    public async Task<List<Paciente>> GetAllAsync() => 
        await _context.Pacientes.Where(p => p.Ativo).OrderBy(p => p.Nome).ToListAsync();

    public async Task<Paciente?> GetByIdAsync(int id) => await _context.Pacientes.FindAsync(id);

    public async Task<int> GetTotalPacientesAsync() => 
        await _context.Pacientes.CountAsync(p => p.Ativo);

    public async Task<int> GetTotalPontosAsync() => 
        await _context.Pacientes.Where(p => p.Ativo).SumAsync(p => p.PsicoPontos);
}
