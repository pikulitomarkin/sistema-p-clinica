using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClinicaPsi.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(AppDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        // Criar banco de dados se não existir
        var connectionString = context.Database.GetConnectionString();
        
        try
        {
            // Garantir que o banco existe (sem migrations)
            await context.Database.EnsureCreatedAsync();
            Console.WriteLine("Database criado/verificado com sucesso!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao criar database: {ex.Message}");
            throw;
        }

        if (connectionString?.Contains("Host=") == true) // PostgreSQL
        {
            
            // Criar roles via Identity (não usa DateTime problemático)
            await CreateRolesAsync(roleManager);
            
            // Criar usuário admin "marcos" usando UserManager
            var marcosUser = new ApplicationUser
            {
                UserName = "marcos@admin.com",
                Email = "marcos@admin.com",
                NomeCompleto = "Marcos Admin",
                TipoUsuario = TipoUsuario.Admin,
                EmailConfirmed = true,
                Ativo = true
            };

            var result = await userManager.CreateAsync(marcosUser, "marcos123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(marcosUser, "Admin");
                Console.WriteLine("Usuario admin marcos criado com sucesso!");
            }
            else
            {
                Console.WriteLine($"Erro ao criar usuario marcos: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
            
            Console.WriteLine("SEED COMPLETO - Somente usuario admin criado");
            return;
        }

        // Criar roles se não existirem
        await CreateRolesAsync(roleManager);

        // Criar usuários padrão se não existirem
        await CreateDefaultUsersAsync(context, userManager);

        // Criar psicólogos padrão se não existirem
        await CreateDefaultPsicologosAsync(context);

        // Criar pacientes padrão se não existirem
        await CreateDefaultPacientesAsync(context);

        // Associar usuários aos psicólogos e pacientes
        await AssociateUsersAsync(context, userManager);

        await context.SaveChangesAsync();
    }

    private static async Task CreateRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        string[] roles = { "Admin", "Psicologo", "Cliente" };

        foreach (string role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }

    private static async Task CreateDefaultUsersAsync(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        // Criar Dra. Ana Santos (Admin)
        if (await userManager.FindByEmailAsync("ana.santos@psii.com") == null)
        {
            var adminUser = new ApplicationUser
            {
                UserName = "ana.santos@psii.com",
                Email = "ana.santos@psii.com",
                NomeCompleto = "Ana Santos",
                TipoUsuario = TipoUsuario.Admin,
                CRP = "08/45168",
                EmailConfirmed = true,
                Ativo = true
            };

            var result = await userManager.CreateAsync(adminUser, "Ana123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }

        // Criar usuário demo para psicólogo
        if (await userManager.FindByEmailAsync("joao.silva@psii.com") == null)
        {
            var psicologoUser = new ApplicationUser
            {
                UserName = "joao.silva@psii.com",
                Email = "joao.silva@psii.com",
                NomeCompleto = "João Silva",
                TipoUsuario = TipoUsuario.Psicologo,
                CRP = "06/123456",
                EmailConfirmed = true,
                Ativo = true
            };

            var result = await userManager.CreateAsync(psicologoUser, "Joao123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(psicologoUser, "Psicologo");
            }
        }

        // Criar usuário demo para cliente
        if (await userManager.FindByEmailAsync("cliente@demo.com") == null)
        {
            var clienteUser = new ApplicationUser
            {
                UserName = "cliente@demo.com",
                Email = "cliente@demo.com",
                NomeCompleto = "Cliente Demo",
                TipoUsuario = TipoUsuario.Cliente,
                CPF = "12345678901",
                EmailConfirmed = true,
                Ativo = true
            };

            var result = await userManager.CreateAsync(clienteUser, "Cliente123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(clienteUser, "Cliente");
            }
        }
    }

    private static async Task CreateDefaultPsicologosAsync(AppDbContext context)
    {
        if (!await context.Psicologos.AnyAsync())
        {
            var psicologos = new List<Psicologo>
            {
                new Psicologo
                {
                    Nome = "Dr. João Silva",
                    Email = "joao.silva@psii.com",
                    CRP = "06/123456",
                    Telefone = "(11) 98765-4321",
                    Especialidades = "TCC, Ansiedade, Depressão",
                    ValorConsulta = 150m,
                    HorarioInicioManha = new TimeSpan(8, 0, 0),
                    HorarioFimManha = new TimeSpan(12, 0, 0),
                    HorarioInicioTarde = new TimeSpan(14, 0, 0),
                    HorarioFimTarde = new TimeSpan(18, 0, 0),
                    AtendeSegunda = true,
                    AtendeTerca = true,
                    AtendeQuarta = true,
                    AtendeQuinta = true,
                    AtendeSexta = true,
                    AtendeSabado = false,
                    AtendeDomingo = false,
                    DataCadastro = DateTime.SpecifyKind(DateTime.Parse("2024-01-01"), DateTimeKind.Utc),
                    Ativo = true
                },
                new Psicologo
                {
                    Nome = "Dra. Ana Santos",
                    Email = "ana.santos@psii.com",
                    CRP = "08/45168",
                    Telefone = "(11) 98765-1234",
                    Especialidades = "Psicanálise, Terapia de Casal, TCC",
                    ValorConsulta = 180m,
                    HorarioInicioManha = new TimeSpan(8, 0, 0),
                    HorarioFimManha = new TimeSpan(12, 0, 0),
                    HorarioInicioTarde = new TimeSpan(14, 0, 0),
                    HorarioFimTarde = new TimeSpan(18, 0, 0),
                    AtendeSegunda = true,
                    AtendeTerca = true,
                    AtendeQuarta = true,
                    AtendeQuinta = true,
                    AtendeSexta = true,
                    AtendeSabado = false,
                    AtendeDomingo = false,
                    DataCadastro = DateTime.SpecifyKind(DateTime.Parse("2024-01-01"), DateTimeKind.Utc),
                    Ativo = true
                }
            };

            context.Psicologos.AddRange(psicologos);
        }
    }

    private static async Task CreateDefaultPacientesAsync(AppDbContext context)
    {
        if (!await context.Pacientes.AnyAsync())
        {
            var pacientes = new List<Paciente>
            {
                new Paciente
                {
                    Nome = "Cliente Demo",
                    Email = "cliente@demo.com",
                    CPF = "12345678901",
                    Telefone = "(11) 91234-5678",
                    DataNascimento = DateTime.SpecifyKind(DateTime.Parse("1990-05-15"), DateTimeKind.Utc),
                    Endereco = "Rua das Flores, 123 - São Paulo, SP",
                    ContatoEmergencia = "Maria Demo",
                    TelefoneEmergencia = "(11) 91234-9876",
                    PsicoPontos = 25,
                    ConsultasRealizadas = 8,
                    ConsultasGratuitas = 2,
                    DataCadastro = DateTime.SpecifyKind(DateTime.Parse("2024-01-15"), DateTimeKind.Utc),
                    Ativo = true
                },
                new Paciente
                {
                    Nome = "João Cliente",
                    Email = "joao.cliente@email.com",
                    CPF = "98765432100",
                    Telefone = "(11) 99876-5432",
                    DataNascimento = DateTime.SpecifyKind(DateTime.Parse("1985-10-20"), DateTimeKind.Utc),
                    Endereco = "Av. Paulista, 456 - São Paulo, SP",
                    ContatoEmergencia = "Ana Cliente",
                    TelefoneEmergencia = "(11) 99876-1234",
                    PsicoPontos = 15,
                    ConsultasRealizadas = 5,
                    ConsultasGratuitas = 1,
                    DataCadastro = DateTime.SpecifyKind(DateTime.Parse("2024-02-01"), DateTimeKind.Utc),
                    Ativo = true
                }
            };

            context.Pacientes.AddRange(pacientes);
        }
    }

    private static async Task AssociateUsersAsync(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        // Associar todos os usuários psicólogos com suas entidades
        var psicologosUsers = await context.Users
            .Where(u => u.TipoUsuario == TipoUsuario.Psicologo && u.PsicologoId == null)
            .ToListAsync();

        foreach (var user in psicologosUsers)
        {
            var psicologo = await context.Psicologos
                .FirstOrDefaultAsync(p => p.Email == user.Email || p.CRP == user.CRP);
            
            if (psicologo != null)
            {
                user.PsicologoId = psicologo.Id;
                await userManager.UpdateAsync(user);
            }
        }

        // Associar todos os usuários clientes com suas entidades
        var clientesUsers = await context.Users
            .Where(u => u.TipoUsuario == TipoUsuario.Cliente && u.PacienteId == null)
            .ToListAsync();

        foreach (var user in clientesUsers)
        {
            var paciente = await context.Pacientes
                .FirstOrDefaultAsync(p => p.Email == user.Email || p.CPF == user.CPF);
            
            if (paciente != null)
            {
                user.PacienteId = paciente.Id;
                await userManager.UpdateAsync(user);
            }
        }
    }
}