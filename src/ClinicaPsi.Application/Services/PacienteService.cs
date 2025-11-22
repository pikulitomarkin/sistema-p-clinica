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

    public async Task<Paciente?> GetByPhoneAsync(string telefone)
    {
        var num = new string(telefone.Where(char.IsDigit).ToArray());
        return await _context.Pacientes.FirstOrDefaultAsync(p => p.Telefone.Contains(num));
    }

    public async Task<Paciente> CreateOrGetByPhoneAsync(string telefone)
    {
        var existente = await GetByPhoneAsync(telefone);
        if (existente != null) return existente;

        var num = new string(telefone.Where(char.IsDigit).ToArray());
        var paciente = new Paciente
        {
            Nome = "Contato WhatsApp",
            Email = string.IsNullOrEmpty(num) ? "whatsapp@local" : $"{num}@whatsapp.local",
            Telefone = num,
            CPF = "00000000000",
            DataNascimento = DateTime.UtcNow.AddYears(-30),
            DataCadastro = DateTime.UtcNow,
            DataCriacao = DateTime.UtcNow,
            Ativo = true
        };

        _context.Pacientes.Add(paciente);
        await _context.SaveChangesAsync();
        return paciente;
    }
}
