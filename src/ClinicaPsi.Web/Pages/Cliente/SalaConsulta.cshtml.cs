using ClinicaPsi.Application.Services;
using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ClinicaPsi.Web.Pages.Cliente;

[Authorize(Roles = "Cliente,Admin")]
public class SalaConsultaModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly VideoConsultaService _videoConsultaService;
    private readonly ILogger<SalaConsultaModel> _logger;

    public SalaConsultaModel(
        AppDbContext context,
        UserManager<ApplicationUser> userManager,
        VideoConsultaService videoConsultaService,
        ILogger<SalaConsultaModel> logger)
    {
        _context = context;
        _userManager = userManager;
        _videoConsultaService = videoConsultaService;
        _logger = logger;
    }

    public Consulta? Consulta { get; set; }
    public string? EmbedUrl { get; set; }
    public string? MensagemErro { get; set; }
    public string? MensagemInfo { get; set; }
    public bool AguardandoChamada { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Forbid();

        Consulta = await _videoConsultaService.ObterConsultaComAcessoAsync(id, user, User.IsInRole("Admin"));

        if (Consulta == null && user.PacienteId.HasValue)
        {
            Consulta = await _context.Consultas
                .Include(c => c.Paciente)
                .Include(c => c.Psicologo)
                .FirstOrDefaultAsync(c => c.Id == id && c.PacienteId == user.PacienteId.Value);
        }

        if (Consulta == null && !string.IsNullOrEmpty(user.Email))
        {
            var email = user.Email.ToLowerInvariant();
            Consulta = await _context.Consultas
                .Include(c => c.Paciente)
                .Include(c => c.Psicologo)
                .FirstOrDefaultAsync(c => c.Id == id && c.Paciente != null &&
                    c.Paciente.Email.ToLower() == email);
        }

        if (Consulta == null)
        {
            MensagemErro = "Consulta não encontrada ou você não tem permissão para acessar esta sala.";
            return Page();
        }

        if (Consulta.Status == StatusConsulta.Cancelada)
        {
            MensagemErro = "Esta consulta foi cancelada.";
            return Page();
        }

        if (!Consulta.PacienteChamado && string.IsNullOrWhiteSpace(Consulta.VideoRoomUrl))
        {
            AguardandoChamada = true;
            MensagemInfo = "Aguarde o psicólogo iniciar a chamada. Esta página atualiza automaticamente.";
            return Page();
        }

        try
        {
            if (Consulta.Formato != FormatoConsulta.Online)
                Consulta.Formato = FormatoConsulta.Online;

            await _videoConsultaService.GarantirSalaAsync(Consulta);
            EmbedUrl = Consulta.VideoRoomUrl;

            if (!Consulta.PacienteChamado)
                MensagemInfo = "Sala disponível. O psicólogo ainda não enviou a chamada formal.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao abrir sala do paciente consulta {Id}", id);
            MensagemErro = "Não foi possível abrir a sala de vídeo.";
        }

        return Page();
    }
}
