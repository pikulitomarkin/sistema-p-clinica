using ClinicaPsi.Application.Services;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace ClinicaPsi.Web.Pages.ConsultaPages;

[Authorize(Roles = "Admin,Psicologo,Cliente")]
public class VideoModel : PageModel
{
    private readonly VideoConsultaService _videoConsultaService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<VideoModel> _logger;

    public VideoModel(
        VideoConsultaService videoConsultaService,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger<VideoModel> logger)
    {
        _videoConsultaService = videoConsultaService;
        _userManager = userManager;
        _configuration = configuration;
        _logger = logger;
    }

    public Consulta? Consulta { get; set; }
    public string? MensagemErro { get; set; }
    public string MeuNome { get; set; } = "Participante";
    public string NomeRemoto { get; set; } = "Participante";
    public string MeuPapel { get; set; } = "Cliente";
    public string? RoomName { get; set; }
    public bool SouPsicologoOuAdmin { get; set; }
    /// <summary>JSON de iceServers para RTCPeerConnection (STUN/TURN).</summary>
    public string IceServersJson { get; set; } = "[]";

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
            RoomName = Consulta.VideoRoomName;
            IceServersJson = BuildIceServersJson();

            SouPsicologoOuAdmin = User.IsInRole("Admin") || User.IsInRole("Psicologo");
            var nomePsicologo = Consulta.Psicologo?.Nome?.Trim();
            var nomePaciente = Consulta.Paciente?.Nome?.Trim();

            if (string.IsNullOrWhiteSpace(nomePsicologo))
                nomePsicologo = "Psicóloga";
            if (string.IsNullOrWhiteSpace(nomePaciente))
                nomePaciente = "Paciente";

            if (SouPsicologoOuAdmin)
            {
                MeuNome = nomePsicologo;
                NomeRemoto = nomePaciente;
                MeuPapel = User.IsInRole("Admin") ? "Admin" : "Psicologo";
            }
            else
            {
                MeuNome = nomePaciente;
                NomeRemoto = nomePsicologo;
                MeuPapel = "Cliente";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao preparar sala de vídeo da consulta {Id}", id);
            MensagemErro = "Não foi possível preparar a sala de vídeo.";
        }

        return Page();
    }

    private string BuildIceServersJson()
    {
        var servers = new List<object>
        {
            new { urls = "stun:stun.l.google.com:19302" },
            new { urls = "stun:stun1.l.google.com:19302" }
        };

        var stunLocal = _configuration["WebRtc:StunUrl"];
        if (!string.IsNullOrWhiteSpace(stunLocal))
            servers.Add(new { urls = stunLocal.Trim() });

        var turnUrl = _configuration["WebRtc:TurnUrl"];
        var turnUser = _configuration["WebRtc:TurnUser"];
        var turnPass = _configuration["WebRtc:TurnPassword"];
        if (!string.IsNullOrWhiteSpace(turnUrl) &&
            !string.IsNullOrWhiteSpace(turnUser) &&
            !string.IsNullOrWhiteSpace(turnPass))
        {
            var baseUrl = turnUrl.Trim();
            servers.Add(new { urls = baseUrl, username = turnUser, credential = turnPass });
            if (!baseUrl.Contains("transport=", StringComparison.OrdinalIgnoreCase))
                servers.Add(new { urls = baseUrl + "?transport=tcp", username = turnUser, credential = turnPass });
        }

        return JsonSerializer.Serialize(servers);
    }
}
