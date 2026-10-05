using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ClinicaPsi.Web.Pages.Admin;

[Authorize(Roles = "Admin")]
public class PrivacidadeModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public PrivacidadeModel(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [BindProperty(SupportsGet = true)]
    public StatusSolicitacaoPrivacidade? FiltroStatus { get; set; }

    public List<SolicitacaoPrivacidade> Solicitacoes { get; private set; } = new();
    public int Pendentes { get; private set; }

    public async Task OnGetAsync()
    {
        await CarregarAsync();
    }

    public async Task<IActionResult> OnPostAtualizarAsync(int id, StatusSolicitacaoPrivacidade status, string? observacao)
    {
        var item = await _db.SolicitacoesPrivacidade.FirstOrDefaultAsync(s => s.Id == id);
        if (item == null)
        {
            TempData["Erro"] = "Solicitação não encontrada.";
            return RedirectToPage();
        }

        var admin = await _userManager.GetUserAsync(User);
        item.Status = status;
        item.ObservacaoAdmin = string.IsNullOrWhiteSpace(observacao) ? item.ObservacaoAdmin : observacao.Trim();
        item.DataAtualizacao = DateTime.UtcNow;
        item.RespondidoPorUserId = admin?.Id;
        await _db.SaveChangesAsync();

        TempData["Sucesso"] = $"Solicitação #{id} atualizada para {status}.";
        return RedirectToPage(new { FiltroStatus });
    }

    private async Task CarregarAsync()
    {
        Pendentes = await _db.SolicitacoesPrivacidade.CountAsync(s =>
            s.Status == StatusSolicitacaoPrivacidade.Pendente ||
            s.Status == StatusSolicitacaoPrivacidade.EmAnalise);

        var q = _db.SolicitacoesPrivacidade.AsNoTracking().AsQueryable();
        if (FiltroStatus.HasValue)
            q = q.Where(s => s.Status == FiltroStatus);

        Solicitacoes = await q
            .OrderByDescending(s => s.DataCriacao)
            .Take(100)
            .ToListAsync();
    }
}
