using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ClinicaPsi.Web.Filters;

/// <summary>
/// Psicólogos com cadastro pendente/recusado só acessam a página de aguardo até o admin validar.
/// </summary>
public class PsicologoValidacaoPageFilter : IAsyncPageFilter
{
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var http = context.HttpContext;
        var user = http.User;
        var path = (http.Request.Path.Value ?? string.Empty).ToLowerInvariant();

        var isPsicologoArea = path.StartsWith("/psicologo")
                              || path.StartsWith("/prontuario")
                              || path.Contains("/video");

        if (isPsicologoArea
            && user.Identity?.IsAuthenticated == true
            && user.IsInRole("Psicologo")
            && !user.IsInRole("Admin")
            && !path.StartsWith("/psicologo/aguardando-validacao")
            && !path.StartsWith("/account/"))
        {
            var userManager = http.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var db = http.RequestServices.GetRequiredService<AppDbContext>();
            var appUser = await userManager.GetUserAsync(user);

            if (appUser != null)
            {
                var psicologo = await db.Psicologos.AsNoTracking()
                    .FirstOrDefaultAsync(p =>
                        p.ExcluidoEm == null &&
                        (p.UserId == appUser.Id
                         || (appUser.PsicologoId != null && p.Id == appUser.PsicologoId)
                         || p.Email == appUser.Email));

                if (psicologo != null && !psicologo.PodeAtender)
                {
                    context.Result = new RedirectResult("/psicologo/aguardando-validacao");
                    return;
                }
            }
        }

        await next();
    }
}
