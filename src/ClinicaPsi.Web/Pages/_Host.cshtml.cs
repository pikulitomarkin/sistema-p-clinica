using ClinicaPsi.Application.Services;
using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ClinicaPsi.Web.Pages;

public class _HostModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ConfiguracaoService _config;

    public _HostModel(AppDbContext db, ConfiguracaoService config)
    {
        _db = db;
        _config = config;
    }

    public ConfiguracaoClinicaView Clinica { get; private set; } = new();
    public List<PsicologoCardVm> Psicologos { get; private set; } = new();
    public List<PsicologoCardVm> Ranking { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var cfg = await _config.ObterConfigSistemaAsync();
        Clinica = new ConfiguracaoClinicaView
        {
            Endereco = cfg.Endereco,
            Telefone = cfg.Telefone,
            Email = cfg.Email,
            HorarioFuncionamento = cfg.HorarioFuncionamento
        };

        var ativos = await _db.Psicologos.AsNoTracking()
            .Where(p => p.Ativo && p.ExcluidoEm == null && p.StatusValidacao == StatusValidacaoPsicologo.Aprovado)
            .OrderBy(p => p.Nome)
            .ToListAsync();

        var psicologoIds = ativos.Select(p => p.Id).ToList();
        var stats = await _db.Avaliacoes.AsNoTracking()
            .Where(a => a.Alvo == TipoAlvoAvaliacao.Psicologo
                        && a.Publica
                        && a.PsicologoId != null
                        && psicologoIds.Contains(a.PsicologoId.Value))
            .GroupBy(a => a.PsicologoId!.Value)
            .Select(g => new
            {
                PsicologoId = g.Key,
                Media = g.Average(x => (double)x.Nota),
                Total = g.Count()
            })
            .ToListAsync();

        var byId = stats.ToDictionary(s => s.PsicologoId);

        Psicologos = ativos.Select(p =>
        {
            byId.TryGetValue(p.Id, out var s);
            return new PsicologoCardVm
            {
                Id = p.Id,
                Nome = p.Nome,
                CRP = p.CRP,
                Especialidades = p.Especialidades,
                FotoUrl = p.FotoUrl,
                ValorConsulta = p.ValorConsulta,
                AvaliacaoMedia = s?.Media ?? 0,
                TotalAvaliacoes = s?.Total ?? 0
            };
        }).ToList();

        Ranking = Psicologos
            .Where(p => p.TotalAvaliacoes > 0)
            .OrderByDescending(p => p.AvaliacaoMedia)
            .ThenByDescending(p => p.TotalAvaliacoes)
            .ThenBy(p => p.Nome)
            .Take(10)
            .ToList();
    }

    public class ConfiguracaoClinicaView
    {
        public string? Endereco { get; set; }
        public string? Telefone { get; set; }
        public string? Email { get; set; }
        public string? HorarioFuncionamento { get; set; }
    }

    public class PsicologoCardVm
    {
        public int Id { get; set; }
        public string Nome { get; set; } = "";
        public string CRP { get; set; } = "";
        public string? Especialidades { get; set; }
        public string? FotoUrl { get; set; }
        public decimal ValorConsulta { get; set; }
        public double AvaliacaoMedia { get; set; }
        public int TotalAvaliacoes { get; set; }
    }
}
