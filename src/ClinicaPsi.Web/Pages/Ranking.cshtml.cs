using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ClinicaPsi.Web.Pages;

public class RankingModel : PageModel
{
    private readonly AppDbContext _db;

    public RankingModel(AppDbContext db) => _db = db;

    public List<RankItem> Itens { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var ativos = await _db.Psicologos.AsNoTracking()
            .Where(p => p.Ativo && p.ExcluidoEm == null)
            .ToListAsync();

        var stats = await _db.Avaliacoes.AsNoTracking()
            .Where(a => a.Alvo == TipoAlvoAvaliacao.Psicologo && a.Publica && a.PsicologoId != null)
            .GroupBy(a => a.PsicologoId!.Value)
            .Select(g => new { Id = g.Key, Media = g.Average(x => (double)x.Nota), Total = g.Count() })
            .ToListAsync();

        var map = stats.ToDictionary(s => s.Id);
        Itens = ativos
            .Select(p =>
            {
                map.TryGetValue(p.Id, out var s);
                return new RankItem
                {
                    Id = p.Id,
                    Nome = p.Nome,
                    CRP = p.CRP,
                    Especialidades = p.Especialidades,
                    FotoUrl = p.FotoUrl,
                    Media = s?.Media ?? 0,
                    Total = s?.Total ?? 0
                };
            })
            .Where(x => x.Total > 0)
            .OrderByDescending(x => x.Media)
            .ThenByDescending(x => x.Total)
            .ThenBy(x => x.Nome)
            .ToList();
    }

    public class RankItem
    {
        public int Id { get; set; }
        public string Nome { get; set; } = "";
        public string CRP { get; set; } = "";
        public string? Especialidades { get; set; }
        public string? FotoUrl { get; set; }
        public double Media { get; set; }
        public int Total { get; set; }
    }
}
