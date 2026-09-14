using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClinicaPsi.Application.Services;

/// <summary>
/// Salas de videochamada 1:1 via WebRTC + SignalR (sem Jitsi / sem conta em provedor).
/// Opcional futuro: DAILY_API_KEY no ambiente para Prebuilt Daily.co.
/// </summary>
public class VideoConsultaService
{
    public const string ConfigHabilitadoKey = "Video.ConsultasOnline.Habilitado";
    public const string ConfigProviderKey = "Video.Provider";
    public const string ProviderWebRtc = "webrtc";

    private readonly AppDbContext _context;
    private readonly ConfiguracaoService _configuracaoService;
    private readonly ILogger<VideoConsultaService> _logger;

    public VideoConsultaService(
        AppDbContext context,
        ConfiguracaoService configuracaoService,
        ILogger<VideoConsultaService> logger)
    {
        _context = context;
        _configuracaoService = configuracaoService;
        _logger = logger;
    }

    public Task<bool> EstaHabilitadoAsync() =>
        _configuracaoService.ObterValorBoolAsync(ConfigHabilitadoKey, true);

    public string ObterProvider()
    {
        // Sem chave de provedor externo: WebRTC mesh 1:1 com SignalR.
        var dailyKey = Environment.GetEnvironmentVariable("DAILY_API_KEY");
        if (!string.IsNullOrWhiteSpace(dailyKey))
            return "daily"; // reservado — UI atual usa WebRTC; Daily pode ser ligado depois
        return ProviderWebRtc;
    }

    public Task<(string RoomName, string RoomUrl)> GerarSalaAsync(int consultaId)
    {
        var token = Guid.NewGuid().ToString("N")[..10];
        var roomName = $"clinicapsi-{consultaId}-{token}";
        // URL interna da aplicação (não é link de provedor externo)
        var roomUrl = $"/consulta/{consultaId}/video";
        return Task.FromResult((roomName, roomUrl));
    }

    public async Task GarantirSalaAsync(Consulta consulta)
    {
        if (consulta.Formato != FormatoConsulta.Online)
            return;

        if (SalaInternaValida(consulta))
            return;

        var idParaSala = consulta.Id > 0 ? consulta.Id : Random.Shared.Next(100000, 999999);
        var (roomName, roomUrl) = await GerarSalaAsync(idParaSala);
        consulta.VideoRoomName = roomName;
        consulta.VideoRoomUrl = roomUrl;
        consulta.DataAtualizacao = DateTime.Now;

        if (consulta.Id > 0)
        {
            _context.Consultas.Update(consulta);
            await _context.SaveChangesAsync();
        }

        _logger.LogInformation(
            "Sala WebRTC gerada para consulta {ConsultaId}: {Room}",
            consulta.Id, roomName);
    }

    public async Task FinalizarSalaAposCriacaoAsync(Consulta consulta)
    {
        if (consulta.Formato != FormatoConsulta.Online || consulta.Id <= 0)
            return;

        if (SalaInternaValida(consulta) &&
            consulta.VideoRoomName!.StartsWith($"clinicapsi-{consulta.Id}-", StringComparison.Ordinal))
            return;

        var (roomName, roomUrl) = await GerarSalaAsync(consulta.Id);
        consulta.VideoRoomName = roomName;
        consulta.VideoRoomUrl = roomUrl;
        consulta.DataAtualizacao = DateTime.Now;
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Descarta salas antigas do Jitsi (meet.jit.si) e regenera URL interna.
    /// </summary>
    public static bool SalaInternaValida(Consulta consulta)
    {
        if (string.IsNullOrWhiteSpace(consulta.VideoRoomName) ||
            string.IsNullOrWhiteSpace(consulta.VideoRoomUrl))
            return false;

        var url = consulta.VideoRoomUrl;
        if (url.Contains("jit.si", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("jitsi", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return false;

        return url.Contains("/video", StringComparison.OrdinalIgnoreCase) ||
               url.Contains("SalaConsulta", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<Consulta?> ObterConsultaComAcessoAsync(int consultaId, ApplicationUser user, bool isAdmin)
    {
        var consulta = await _context.Consultas
            .Include(c => c.Paciente)
            .Include(c => c.Psicologo)
            .FirstOrDefaultAsync(c => c.Id == consultaId);
        if (consulta == null) return null;
        if (isAdmin) return consulta;
        if (user.PsicologoId.HasValue && consulta.PsicologoId == user.PsicologoId.Value) return consulta;
        if (user.PacienteId.HasValue && consulta.PacienteId == user.PacienteId.Value) return consulta;
        if (!string.IsNullOrEmpty(user.Email) && consulta.Psicologo?.Email != null &&
            string.Equals(consulta.Psicologo.Email, user.Email, StringComparison.OrdinalIgnoreCase))
            return consulta;
        return null;
    }
}
