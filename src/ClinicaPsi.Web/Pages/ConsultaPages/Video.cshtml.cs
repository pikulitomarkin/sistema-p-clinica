using ClinicaPsi.Application.Services;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClinicaPsi.Web.Pages.ConsultaPages;

[Authorize(Roles = "Admin,Psicologo,Cliente")]
public class VideoModel : PageModel
{
    private readonly VideoConsultaService _videoConsultaService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<VideoModel> _logger;

    public VideoModel(VideoConsultaService videoConsultaService, UserManager<ApplicationUser> userManager, ILogger<VideoModel> logger)
    {
        _videoConsultaService = videoConsultaService;
        _userManager = userManager;
        _logger = logger;
    }

    public Consulta? Consulta { get; set; }
    public string? EmbedUrl { get; set; }
    public string? MensagemErro { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await _videoConsultaService.EstaHabilitadoAsync())
        {
            MensagemErro = "Videochamadas online estão desabilitadas nas configurações do sistema.";
            return Page();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Forbid();

        Consulta = await _videoConsultaService.ObterConsultaComAcessoAsync(id, user, User.IsInRole("Admin"));
        if (Consulta == null)
        {
            MensagemErro = "Consulta não encontrada ou você não tem permissão para acessar esta sala.";
            return Page();
        }
        if (Consulta.Formato != FormatoConsulta.Online)
        {
            MensagemErro = "Esta consulta não é online e não possui sala de vídeo.";
            return Page();
        }
        if (Consulta.Status == StatusConsulta.Cancelada)
        {
            MensagemErro = "Esta consulta foi cancelada.";
            return Page();
        }

        try
        {
            await _videoConsultaService.GarantirSalaAsync(Consulta);
            EmbedUrl = Consulta.VideoRoomUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao preparar sala de vídeo da consulta {Id}", id);
            MensagemErro = "Não foi possível preparar a sala de vídeo.";
        }
        return Page();
    }
}
