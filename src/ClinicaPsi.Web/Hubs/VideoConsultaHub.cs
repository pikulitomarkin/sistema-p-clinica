using System.Collections.Concurrent;
using ClinicaPsi.Application.Services;
using ClinicaPsi.Application.Services.Email;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;

namespace ClinicaPsi.Web.Hubs;

/// <summary>
/// Sinalização WebRTC 1:1 (oferta/answer/ICE) por consulta — sem conta em provedor externo.
/// Também notifica o paciente fora da sala quando o psicólogo chama.
/// </summary>
[Authorize(Roles = "Admin,Psicologo,Cliente")]
public class VideoConsultaHub : Hub
{
    private static readonly ConcurrentDictionary<string, ParticipantInfo> Participants = new();

    private readonly VideoConsultaService _videoConsultaService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<VideoConsultaHub> _logger;

    public VideoConsultaHub(
        VideoConsultaService videoConsultaService,
        UserManager<ApplicationUser> userManager,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<VideoConsultaHub> logger)
    {
        _videoConsultaService = videoConsultaService;
        _userManager = userManager;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>Cliente (paciente) entra no grupo de notificações globais da área logada.</summary>
    public async Task SubscribePacienteNotificacoes()
    {
        var user = await _userManager.GetUserAsync(Context.User!);
        if (user?.PacienteId == null)
            return;

        var group = PacienteGroup(user.PacienteId.Value);
        await Groups.AddToGroupAsync(Context.ConnectionId, group);
        _logger.LogDebug("Paciente {PacienteId} inscrito em notificações ({Conn})", user.PacienteId, Context.ConnectionId);
    }

    public async Task JoinRoom(int consultaId, string roomName, string displayName, string role)
    {
        var user = await _userManager.GetUserAsync(Context.User!);
        if (user == null)
            throw new HubException("Usuário não autenticado.");

        var isAdmin = Context.User!.IsInRole("Admin");
        var consulta = await _videoConsultaService.ObterConsultaComAcessoAsync(consultaId, user, isAdmin);
        if (consulta == null || consulta.Formato != FormatoConsulta.Online)
            throw new HubException("Sem permissão para esta sala.");

        if (string.IsNullOrWhiteSpace(consulta.VideoRoomName) ||
            !string.Equals(consulta.VideoRoomName, roomName, StringComparison.Ordinal))
            throw new HubException("Sala inválida ou expirada.");

        var safeName = string.IsNullOrWhiteSpace(displayName) ? "Participante" : displayName.Trim();
        if (safeName.Length > 120) safeName = safeName[..120];

        var safeRole = role?.Trim() switch
        {
            "Psicologo" => "Psicologo",
            "Cliente" => "Cliente",
            "Admin" => "Admin",
            _ => Context.User.IsInRole("Psicologo") ? "Psicologo" : "Cliente"
        };

        if (Participants.TryRemove(Context.ConnectionId, out var previous))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, previous.RoomName);

        Participants[Context.ConnectionId] = new ParticipantInfo(consultaId, roomName, safeName, safeRole);
        await Groups.AddToGroupAsync(Context.ConnectionId, roomName);

        // Paciente entrou na sala: encerra flag de notificação in-app
        if (safeRole == "Cliente")
            await _videoConsultaService.EncerrarChamadaPacienteAsync(consultaId);

        var peers = Participants
            .Where(p => p.Key != Context.ConnectionId && p.Value.RoomName == roomName)
            .Select(p => new { connectionId = p.Key, displayName = p.Value.DisplayName, role = p.Value.Role })
            .ToList();

        await Clients.Caller.SendAsync("RoomJoined", new
        {
            roomName,
            displayName = safeName,
            role = safeRole,
            peers
        });

        await Clients.OthersInGroup(roomName).SendAsync("PeerJoined", new
        {
            connectionId = Context.ConnectionId,
            displayName = safeName,
            role = safeRole
        });

        _logger.LogInformation(
            "Video room {Room}: {Name} ({Role}) entrou. Peers={Count}",
            roomName, safeName, safeRole, peers.Count);
    }

    public async Task CallPeer()
    {
        if (!Participants.TryGetValue(Context.ConnectionId, out var me))
            return;

        // Notifica quem já está na sala
        await Clients.OthersInGroup(me.RoomName).SendAsync("IncomingCall", new
        {
            connectionId = Context.ConnectionId,
            displayName = me.DisplayName,
            role = me.Role
        });

        // Persiste flag + notifica paciente em qualquer página da área Cliente
        try
        {
            var (consulta, novaChamada) = await _videoConsultaService.RegistrarChamadaPacienteAsync(me.ConsultaId);
            var videoUrl = string.IsNullOrWhiteSpace(consulta.VideoRoomUrl)
                ? $"/consulta/{consulta.Id}/video"
                : consulta.VideoRoomUrl!;

            var payload = new
            {
                consultaId = consulta.Id,
                psicologoNome = consulta.Psicologo?.Nome ?? me.DisplayName,
                videoUrl,
                chamadaEm = consulta.VideoChamadaAtivaEm,
                dataHorario = consulta.DataHorario
            };

            await Clients.Group(PacienteGroup(consulta.PacienteId)).SendAsync("ChamadaRecebida", payload);

            if (novaChamada)
                await TentarEnviarEmailChamadaAsync(consulta, videoUrl);

            _logger.LogInformation(
                "Chamada registrada consulta={ConsultaId} paciente={PacienteId} nova={Nova}",
                consulta.Id, consulta.PacienteId, novaChamada);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao registrar/notificar chamada da consulta {ConsultaId}", me.ConsultaId);
        }
    }

    public async Task SendOffer(string targetConnectionId, string sdp)
    {
        if (!CanSignal(targetConnectionId, out var me))
        {
            _logger.LogWarning(
                "SendOffer bloqueado: caller={Caller} target={Target} (fora da mesma sala ou ausente)",
                Context.ConnectionId, targetConnectionId);
            return;
        }

        _logger.LogInformation(
            "WebRTC Offer {From} ({Name}) → {To} sdpLen={Len}",
            Context.ConnectionId, me!.DisplayName, targetConnectionId, sdp?.Length ?? 0);

        await Clients.Client(targetConnectionId).SendAsync("ReceiveOffer", new
        {
            fromConnectionId = Context.ConnectionId,
            displayName = me.DisplayName,
            role = me.Role,
            sdp
        });
    }

    public async Task SendAnswer(string targetConnectionId, string sdp)
    {
        if (!CanSignal(targetConnectionId, out var me))
        {
            _logger.LogWarning(
                "SendAnswer bloqueado: caller={Caller} target={Target}",
                Context.ConnectionId, targetConnectionId);
            return;
        }

        _logger.LogInformation(
            "WebRTC Answer {From} ({Name}) → {To} sdpLen={Len}",
            Context.ConnectionId, me!.DisplayName, targetConnectionId, sdp?.Length ?? 0);

        await Clients.Client(targetConnectionId).SendAsync("ReceiveAnswer", new
        {
            fromConnectionId = Context.ConnectionId,
            displayName = me.DisplayName,
            sdp
        });
    }

    public async Task SendIceCandidate(string targetConnectionId, string candidateJson)
    {
        if (!CanSignal(targetConnectionId, out _))
            return;

        _logger.LogDebug(
            "WebRTC ICE {From} → {To} len={Len}",
            Context.ConnectionId, targetConnectionId, candidateJson?.Length ?? 0);

        await Clients.Client(targetConnectionId).SendAsync("ReceiveIceCandidate", new
        {
            fromConnectionId = Context.ConnectionId,
            candidate = candidateJson
        });
    }

    public async Task LeaveRoom()
    {
        await RemoveFromRoomAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await RemoveFromRoomAsync();
        await base.OnDisconnectedAsync(exception);
    }

    private async Task TentarEnviarEmailChamadaAsync(Consulta consulta, string videoUrl)
    {
        if (!_emailService.IsConfigured)
            return;

        var email = consulta.Paciente?.Email;
        if (string.IsNullOrWhiteSpace(email))
            return;

        try
        {
            var baseUrl = (_configuration["PUBLIC_APP_URL"]
                           ?? _configuration["WhatsApp:SiteUrl"]
                           ?? "").TrimEnd('/');
            var link = videoUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? videoUrl
                : $"{baseUrl}{videoUrl}";

            var psicologo = consulta.Psicologo?.Nome ?? "seu(sua) psicólogo(a)";
            var html = $@"
<p>Olá{(string.IsNullOrWhiteSpace(consulta.Paciente?.Nome) ? "" : $", {consulta.Paciente.Nome}")}!</p>
<p><strong>{psicologo}</strong> está chamando você para a consulta online.</p>
<p><a href=""{link}"" style=""display:inline-block;padding:12px 20px;background:#28a745;color:#fff;text-decoration:none;border-radius:6px;"">Entrar na chamada</a></p>
<p>Ou acesse: {link}</p>";

            await _emailService.SendAsync(
                email,
                "Você está sendo chamado(a) para a videochamada",
                html);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao enviar e-mail de chamada consulta {Id}", consulta.Id);
        }
    }

    private bool CanSignal(string targetConnectionId, out ParticipantInfo? me)
    {
        me = null;
        if (!Participants.TryGetValue(Context.ConnectionId, out me))
            return false;
        if (!Participants.TryGetValue(targetConnectionId, out var peer))
            return false;
        return string.Equals(me.RoomName, peer.RoomName, StringComparison.Ordinal);
    }

    private async Task RemoveFromRoomAsync()
    {
        if (!Participants.TryRemove(Context.ConnectionId, out var me))
            return;

        // Se psicólogo sai, encerra notificação pendente
        if (me.Role is "Psicologo" or "Admin")
        {
            try { await _videoConsultaService.EncerrarChamadaPacienteAsync(me.ConsultaId); }
            catch (Exception ex) { _logger.LogWarning(ex, "Erro ao encerrar flag de chamada {Id}", me.ConsultaId); }
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, me.RoomName);
        await Clients.OthersInGroup(me.RoomName).SendAsync("PeerLeft", new
        {
            connectionId = Context.ConnectionId,
            displayName = me.DisplayName
        });
    }

    private static string PacienteGroup(int pacienteId) => $"paciente-{pacienteId}";

    private sealed record ParticipantInfo(int ConsultaId, string RoomName, string DisplayName, string Role);
}
