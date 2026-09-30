using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ClinicaPsi.Web.Pages.Psicologo;

[Authorize(Roles = "Psicologo,Admin")]
public class AguardandoValidacaoModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AguardandoValidacaoModel(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public ClinicaPsi.Shared.Models.Psicologo? Psicologo { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToPage("/Account/Login");

        Psicologo = await _db.Psicologos.AsNoTracking()
            .FirstOrDefaultAsync(p =>
                p.ExcluidoEm == null &&
                (p.UserId == user.Id
                 || (user.PsicologoId != null && p.Id == user.PsicologoId)
                 || p.Email == user.Email));

        if (Psicologo == null)
            return Redirect("/psicologo");

        if (Psicologo.PodeAtender)
            return Redirect("/psicologo");

        return Page();
    }
}
