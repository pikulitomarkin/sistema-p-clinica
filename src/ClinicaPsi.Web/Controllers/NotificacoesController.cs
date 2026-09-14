using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicaPsi.Web.Controllers;

[ApiController]
[Route("api/notificacoes")]
[Authorize(Roles = "Cliente,Admin")]
public class NotificacoesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public NotificacoesController(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet("chamadas")]
    public async Task<IActionResult> ObterChamadas([FromQuery] DateTime? desde = null)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized();

        int? pacienteId = user.PacienteId;
        if (pacienteId == null && !string.IsNullOrEmpty(user.Email))
        {
            var email = user.Email.ToLowerInvariant();
            pacienteId = await _context.Pacientes
                .Where(p => p.Email.ToLower() == email)
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync();
        }

        if (pacienteId == null)
            return Ok(new { chamadas = Array.Empty<object>() });

        var corte = desde ?? DateTime.Now.AddHours(-4);

        var chamadas = await _context.Consultas
            .AsNoTracking()
            .Include(c => c.Psicologo)
            .Where(c => c.PacienteId == pacienteId.Value
                        && c.PacienteChamado
                        && c.PacienteChamadoEm != null
                        && c.PacienteChamadoEm >= corte
                        && c.Status != StatusConsulta.Cancelada
                        && c.Status != StatusConsulta.Realizada)
            .OrderByDescending(c => c.PacienteChamadoEm)
            .Take(5)
            .Select(c => new
            {
                consultaId = c.Id,
                psicologo = c.Psicologo != null ? c.Psicologo.Nome : "Psicólogo(a)",
                dataHorario = c.DataHorario,
                chamadoEm = c.PacienteChamadoEm,
                url = $"/Cliente/SalaConsulta/{c.Id}"
            })
            .ToListAsync();

        return Ok(new { chamadas });
    }
}
