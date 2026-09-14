using System.Collections.Concurrent;
using ClinicaPsi.Application.Services;
using ClinicaPsi.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;

namespace ClinicaPsi.Web.Hubs;

/// <summary>
/// Sinalização WebRTC 1:1 (oferta/answer/ICE) por consulta — sem conta em provedor externo.
/// </summary>
[Authorize(Roles = "Admin,Psicologo,Cliente")]
public class VideoConsultaHub : Hub
{
    private static readonly ConcurrentDictionary<string, ParticipantInfo> Participants = new();

    private readonly VideoConsultaService _videoConsultaService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<VideoConsultaHub> _logger;

    public VideoConsultaHub(
        VideoConsultaService videoConsultaService,
        UserManager<ApplicationUser> userManager,
        ILogger<VideoConsultaHub> logger)
    {
        _videoConsultaService = videoConsultaService;
        _userManager = userManager;
        _logger = logger;
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

        // Remove participação anterior deste connection
        if (Participants.TryRemove(Context.ConnectionId, out var previous))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, previous.RoomName);

        Participants[Context.ConnectionId] = new ParticipantInfo(consultaId, roomName, safeName, safeRole);
        await Groups.AddToGroupAsync(Context.ConnectionId, roomName);

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

        await Clients.OthersInGroup(me.RoomName).SendAsync("IncomingCall", new
        {
            connectionId = Context.ConnectionId,
            displayName = me.DisplayName,
            role = me.Role
        });
    }

    public async Task SendOffer(string targetConnectionId, string sdp)
    {
        if (!CanSignal(targetConnectionId, out var me))
            return;

        await Clients.Client(targetConnectionId).SendAsync("ReceiveOffer", new
        {
            fromConnectionId = Context.ConnectionId,
            displayName = me!.DisplayName,
            role = me.Role,
            sdp
        });
    }

    public async Task SendAnswer(string targetConnectionId, string sdp)
    {
        if (!CanSignal(targetConnectionId, out var me))
            return;

        await Clients.Client(targetConnectionId).SendAsync("ReceiveAnswer", new
        {
            fromConnectionId = Context.ConnectionId,
            displayName = me!.DisplayName,
            sdp
        });
    }

    public async Task SendIceCandidate(string targetConnectionId, string candidateJson)
    {
        if (!CanSignal(targetConnectionId, out _))
            return;

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

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, me.RoomName);
        await Clients.OthersInGroup(me.RoomName).SendAsync("PeerLeft", new
        {
            connectionId = Context.ConnectionId,
            displayName = me.DisplayName
        });
    }

    private sealed record ParticipantInfo(int ConsultaId, string RoomName, string DisplayName, string Role);
}
