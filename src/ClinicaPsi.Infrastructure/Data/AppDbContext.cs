using ClinicaPsi.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace ClinicaPsi.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Paciente> Pacientes => Set<Paciente>();
    public DbSet<Psicologo> Psicologos => Set<Psicologo>();
    public DbSet<Consulta> Consultas => Set<Consulta>();
    public DbSet<HistoricoPontos> HistoricoPontos => Set<HistoricoPontos>();
    public DbSet<NotificacaoConsulta> NotificacoesConsultas => Set<NotificacaoConsulta>();
    public DbSet<AuditoriaUsuario> AuditoriasUsuarios => Set<AuditoriaUsuario>();
    public DbSet<ConfiguracaoSistema> ConfiguracoesSistema => Set<ConfiguracaoSistema>();
    public DbSet<ProntuarioEletronico> ProntuariosEletronicos => Set<ProntuarioEletronico>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Paciente>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CPF).IsUnique();
            entity.HasIndex(e => e.Email);
            entity.Property(e => e.Nome).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
            entity.Property(e => e.CPF).IsRequired().HasMaxLength(11);
            entity.Property(e => e.DataCadastro).HasDefaultValueSql("datetime('now')");
        });

        modelBuilder.Entity<Psicologo>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CRP).IsUnique();
            entity.HasIndex(e => e.Email);
            entity.Property(e => e.Nome).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ValorConsulta).HasPrecision(10, 2);
            entity.Property(e => e.DataCadastro).HasDefaultValueSql("datetime('now')");
        });

        modelBuilder.Entity<Consulta>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Valor).HasPrecision(10, 2);
            entity.Property(e => e.DataAgendamento).HasDefaultValueSql("datetime('now')");
            
            entity.HasOne(e => e.Paciente)
                .WithMany(p => p.Consultas)
                .HasForeignKey(e => e.PacienteId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Psicologo)
                .WithMany(p => p.Consultas)
                .HasForeignKey(e => e.PsicologoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.DataHorario);
        });

        modelBuilder.Entity<HistoricoPontos>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DataMovimentacao).HasDefaultValueSql("datetime('now')");
            
            entity.HasOne(e => e.Paciente)
                .WithMany(p => p.HistoricoPontos)
                .HasForeignKey(e => e.PacienteId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Consulta)
                .WithMany()
                .HasForeignKey(e => e.ConsultaId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<NotificacaoConsulta>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DataEnvio).HasDefaultValueSql("datetime('now')");
            
            entity.HasOne(e => e.Consulta)
                .WithMany(c => c.Notificacoes)
                .HasForeignKey(e => e.ConsultaId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Dados iniciais para teste
        SeedData(modelBuilder);
    }

    private void SeedData(ModelBuilder modelBuilder)
    {
        var dataAtual = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        
        modelBuilder.Entity<Psicologo>().HasData(
            new Psicologo
            {
                Id = 1,
                Nome = "Dr. João Silva",
                Email = "joao.silva@clinicapsi.com",
                CRP = "06/123456",
                Telefone = "(11) 98765-4321",
                Especialidades = "TCC, Ansiedade, Depressão",
                ValorConsulta = 150.00m,
                DataCadastro = dataAtual,
                Ativo = true
            },
            new Psicologo
            {
                Id = 2,
                Nome = "Dra. Maria Santos",
                Email = "maria.santos@clinicapsi.com",
                CRP = "06/654321",
                Telefone = "(11) 98765-1234",
                Especialidades = "Psicanálise, Terapia de Casal",
                ValorConsulta = 180.00m,
                DataCadastro = dataAtual,
                Ativo = true
            }
        );

        modelBuilder.Entity<AuditoriaUsuario>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AdminId);
            entity.HasIndex(e => e.UsuarioAfetadoId);
            entity.HasIndex(e => e.DataHora);
            entity.Property(e => e.AdminNome).IsRequired().HasMaxLength(200);
            entity.Property(e => e.UsuarioAfetadoNome).IsRequired().HasMaxLength(200);
            entity.Property(e => e.UsuarioAfetadoEmail).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<ConfiguracaoSistema>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Chave).IsUnique();
            entity.HasIndex(e => e.Categoria);
            entity.Property(e => e.Chave).IsRequired().HasMaxLength(100);
            entity.Property(e => e.TipoValor).IsRequired().HasMaxLength(20);
        });

        modelBuilder.Entity<ProntuarioEletronico>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PacienteId);
            entity.HasIndex(e => e.PsicologoId);
            entity.HasIndex(e => e.ConsultaId);
            entity.HasIndex(e => e.DataSessao);
            
            entity.HasOne(e => e.Paciente)
                .WithMany()
                .HasForeignKey(e => e.PacienteId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(e => e.Psicologo)
                .WithMany()
                .HasForeignKey(e => e.PsicologoId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(e => e.Consulta)
                .WithMany()
                .HasForeignKey(e => e.ConsultaId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
