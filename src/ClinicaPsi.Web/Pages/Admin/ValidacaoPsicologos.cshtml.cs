using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ClinicaPsi.Web.Pages.Admin;

[Authorize(Roles = "Admin")]
public class ValidacaoPsicologosModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public ValidacaoPsicologosModel(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public List<ClinicaPsi.Shared.Models.Psicologo> Pendentes { get; private set; } = new();
    public List<ClinicaPsi.Shared.Models.Psicologo> Recentes { get; private set; } = new();

    [BindProperty]
    public string? MotivoRecusa { get; set; }

    public async Task OnGetAsync()
    {
        Pendentes = await _db.Psicologos.AsNoTracking()
            .Where(p => p.ExcluidoEm == null && p.StatusValidacao == StatusValidacaoPsicologo.Pendente)
            .OrderBy(p => p.DataCadastro)
            .ToListAsync();

        Recentes = await _db.Psicologos.AsNoTracking()
            .Where(p => p.ExcluidoEm == null && p.StatusValidacao != StatusValidacaoPsicologo.Pendente
                        && (p.ValidadoEm != null || p.DocumentoCnhUrl != null))
            .OrderByDescending(p => p.ValidadoEm ?? p.DataAtualizacao ?? p.DataCadastro)
            .Take(20)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostAprovarAsync(int id)
    {
        var psicologo = await _db.Psicologos.FirstOrDefaultAsync(p => p.Id == id && p.ExcluidoEm == null);
        if (psicologo == null)
        {
            TempData["Error"] = "Psicólogo não encontrado.";
            return RedirectToPage();
        }

        var admin = await _userManager.GetUserAsync(User);
        psicologo.StatusValidacao = StatusValidacaoPsicologo.Aprovado;
        psicologo.Ativo = true;
        psicologo.ValorConsulta = psicologo.ValorContratoConsulta > 0 ? psicologo.ValorContratoConsulta : 50m;
        psicologo.ValidadoEm = DateTime.UtcNow;
        psicologo.ValidadoPorUserId = admin?.Id;
        psicologo.MotivoRecusa = null;
        psicologo.DataAtualizacao = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Sucesso"] = $"{psicologo.Nome} aprovado e liberado para atender.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRecusarAsync(int id)
    {
        var psicologo = await _db.Psicologos.FirstOrDefaultAsync(p => p.Id == id && p.ExcluidoEm == null);
        if (psicologo == null)
        {
            TempData["Error"] = "Psicólogo não encontrado.";
            return RedirectToPage();
        }

        var admin = await _userManager.GetUserAsync(User);
        psicologo.StatusValidacao = StatusValidacaoPsicologo.Recusado;
        psicologo.Ativo = false;
        psicologo.ValidadoEm = DateTime.UtcNow;
        psicologo.ValidadoPorUserId = admin?.Id;
        psicologo.MotivoRecusa = string.IsNullOrWhiteSpace(MotivoRecusa)
            ? "Documentação insuficiente ou inconsistente."
            : MotivoRecusa.Trim();
        psicologo.DataAtualizacao = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Warning"] = $"Cadastro de {psicologo.Nome} recusado.";
        return RedirectToPage();
    }
}
