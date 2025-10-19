using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using ClinicaPsi.Shared.Models;

namespace ClinicaPsi.Data.Context;

public class ClinicaPsiDbContext : IdentityDbContext
{
    public ClinicaPsiDbContext(DbContextOptions<ClinicaPsiDbContext> options) : base(options)
    {
    }

    // DbSets
    public DbSet<Paciente> Pacientes { get; set; }
    public DbSet<Psicologo> Psicologos { get; set; }
    public DbSet<Consulta> Consultas { get; set; }
    public DbSet<HistoricoPontos> HistoricoPontos { get; set; }
    public DbSet<NotificacaoConsulta> NotificacoesConsulta { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuração da entidade Paciente
        modelBuilder.Entity<Paciente>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Nome).IsRequired().HasMaxLength(100);
            entity.Property(p => p.Email).IsRequired().HasMaxLength(100);
            entity.Property(p => p.CPF).IsRequired().HasMaxLength(11);
            entity.Property(p => p.Telefone).IsRequired().HasMaxLength(20);
            
            entity.HasIndex(p => p.Email).IsUnique();
            entity.HasIndex(p => p.CPF).IsUnique();
            
            entity.HasMany(p => p.Consultas)
                  .WithOne(c => c.Paciente)
                  .HasForeignKey(c => c.PacienteId)
                  .OnDelete(DeleteBehavior.Restrict);
                  
            entity.HasMany(p => p.HistoricoPontos)
                  .WithOne(h => h.Paciente)
                  .HasForeignKey(h => h.PacienteId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configuração da entidade Psicologo
        modelBuilder.Entity<Psicologo>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Nome).IsRequired().HasMaxLength(100);
            entity.Property(p => p.Email).IsRequired().HasMaxLength(100);
            entity.Property(p => p.CRP).IsRequired().HasMaxLength(20);
            entity.Property(p => p.ValorConsulta).HasColumnType("decimal(10,2)");
            
            entity.HasIndex(p => p.Email).IsUnique();
            entity.HasIndex(p => p.CRP).IsUnique();
            
            entity.HasMany(p => p.Consultas)
                  .WithOne(c => c.Psicologo)
                  .HasForeignKey(c => c.PsicologoId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Configuração da entidade Consulta
        modelBuilder.Entity<Consulta>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Valor).HasColumnType("decimal(10,2)");
            
            entity.HasIndex(c => new { c.PsicologoId, c.DataHorario }).IsUnique();
            
            entity.HasMany(c => c.Notificacoes)
                  .WithOne(n => n.Consulta)
                  .HasForeignKey(n => n.ConsultaId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configuração da entidade HistoricoPontos
        modelBuilder.Entity<HistoricoPontos>(entity =>
        {
            entity.HasKey(h => h.Id);
            entity.Property(h => h.Motivo).IsRequired().HasMaxLength(200);
            
            entity.HasOne(h => h.Consulta)
                  .WithMany()
                  .HasForeignKey(h => h.ConsultaId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Configuração da entidade NotificacaoConsulta
        modelBuilder.Entity<NotificacaoConsulta>(entity =>
        {
            entity.HasKey(n => n.Id);
            entity.Property(n => n.Destinatario).IsRequired().HasMaxLength(100);
            entity.Property(n => n.Assunto).IsRequired().HasMaxLength(200);
            entity.Property(n => n.Conteudo).IsRequired();
        });

        // Dados iniciais (Seed Data)
        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        // Psicólogo padrão
        modelBuilder.Entity<Psicologo>().HasData(
            new Psicologo
            {
                Id = 1,
                Nome = "Dr. João Silva",
                Email = "joao.silva@clinicapsi.com",
                CRP = "12345",
                Telefone = "(11) 99999-9999",
                Especialidades = "Terapia Cognitivo-Comportamental, Ansiedade, Depressão",
                ValorConsulta = 150.00m,
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
                DataCadastro = DateTime.Now,
                Ativo = true
            }
        );

        // Paciente de exemplo
        modelBuilder.Entity<Paciente>().HasData(
            new Paciente
            {
                Id = 1,
                Nome = "Maria Santos",
                Email = "maria.santos@email.com",
                Telefone = "(11) 88888-8888",
                CPF = "12345678901",
                DataNascimento = new DateTime(1990, 5, 15),
                Endereco = "Rua das Flores, 123 - São Paulo/SP",
                ContatoEmergencia = "José Santos",
                TelefoneEmergencia = "(11) 77777-7777",
                PsicoPontos = 5,
                ConsultasRealizadas = 5,
                DataCadastro = DateTime.Now,
                Ativo = true
            }
        );
    }
}