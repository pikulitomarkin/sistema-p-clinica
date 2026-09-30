using ClinicaPsi.Application.Services;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ClinicaPsi.Web.Controllers;

[Authorize(Roles = "Cliente")]
[ApiController]
[Route("api/notificacoes")]
public class NotificacoesController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly VideoConsultaService _videoConsultaService;
    private readonly ILogger<NotificacoesController> _logger;

    public NotificacoesController(
        UserManager<ApplicationUser> userManager,
        VideoConsultaService videoConsultaService,
        ILogger<NotificacoesController> logger)
    {
        _userManager = userManager;
        _videoConsultaService = videoConsultaService;
        _logger = logger;
    }

    /// <summary>Chamadas de video ativas do paciente logado (fallback de polling).</summary>
    [HttpGet("chamadas")]
    public async Task<IActionResult> GetChamadasAtivas()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user?.PacienteId == null)
            return Ok(Array.Empty<object>());

        try
        {
            var chamadas = await _videoConsultaService.ObterChamadasAtivasDoPacienteAsync(user.PacienteId.Value);
            return Ok(chamadas.Select(c => new
            {
                consultaId = c.ConsultaId,
                psicologoNome = c.PsicologoNome,
                videoUrl = c.VideoUrl,
                chamadaEm = c.ChamadaEm,
                dataHorario = c.DataHorario
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar chamadas ativas do paciente {PacienteId}", user.PacienteId);
            return Ok(Array.Empty<object>());
        }
    }
}
