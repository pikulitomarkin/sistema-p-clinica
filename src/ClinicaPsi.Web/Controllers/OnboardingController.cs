using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ClinicaPsi.Web.Controllers;

[Authorize(Roles = "Cliente,Psicologo")]
[ApiController]
[Route("api/onboarding")]
public class OnboardingController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<OnboardingController> _logger;

    public OnboardingController(
        UserManager<ApplicationUser> userManager,
        ILogger<OnboardingController> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        var role = ResolveRole();
        if (role is null)
            return Ok(new { show = false, completed = true, role = (string?)null });

        return Ok(new
        {
            show = !user.OnboardingCompleted,
            completed = user.OnboardingCompleted,
            role,
            nome = string.IsNullOrWhiteSpace(user.NomeCompleto) ? user.Email : user.NomeCompleto
        });
    }

    [HttpPost("complete")]
    [IgnoreAntiforgeryToken]
    public Task<IActionResult> Complete() => SetCompletedAsync(true);

    [HttpPost("dismiss")]
    [IgnoreAntiforgeryToken]
    public Task<IActionResult> Dismiss() => SetCompletedAsync(true);

    [HttpPost("reset")]
    [IgnoreAntiforgeryToken]
    public Task<IActionResult> Reset() => SetCompletedAsync(false);

    private async Task<IActionResult> SetCompletedAsync(bool completed)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        if (ResolveRole() is null)
            return Forbid();

        user.OnboardingCompleted = completed;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            _logger.LogWarning("Falha ao atualizar onboarding de {UserId}: {Errors}",
                user.Id, string.Join("; ", result.Errors.Select(e => e.Description)));
            return BadRequest(new { ok = false });
        }

        return Ok(new { ok = true, completed });
    }

    private string? ResolveRole()
    {
        if (User.IsInRole("Psicologo"))
            return "psicologo";
        if (User.IsInRole("Cliente"))
            return "cliente";
        return null;
    }
}
