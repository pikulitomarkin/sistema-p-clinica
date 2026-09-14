using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClinicaPsi.Application.Services;

public class VideoConsultaService
{
    public const string ConfigBaseUrlKey = "Video.JitsiBaseUrl";
    public const string ConfigHabilitadoKey = "Video.ConsultasOnline.Habilitado";
    public const string DefaultJitsiBaseUrl = "https://meet.jit.si";

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

    public async Task<string> ObterBaseUrlAsync()
    {
        var url = await _configuracaoService.ObterValorStringAsync(ConfigBaseUrlKey, DefaultJitsiBaseUrl);
        return string.IsNullOrWhiteSpace(url) ? DefaultJitsiBaseUrl : url.TrimEnd('/');
    }

    public async Task<(string RoomName, string RoomUrl)> GerarSalaAsync(int consultaId)
    {
        var baseUrl = await ObterBaseUrlAsync();
        var roomName = $"clinicapsi-{consultaId}-{Guid.NewGuid().ToString("N")[..8]}";
        return (roomName, $"{baseUrl}/{roomName}");
    }

    public async Task GarantirSalaAsync(Consulta consulta)
    {
        if (consulta.Formato != FormatoConsulta.Online)
            return;
        if (!string.IsNullOrWhiteSpace(consulta.VideoRoomUrl) && !string.IsNullOrWhiteSpace(consulta.VideoRoomName))
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

        _logger.LogInformation("Sala de vídeo gerada para consulta {ConsultaId}: {Room}", consulta.Id, roomName);
    }

    public async Task FinalizarSalaAposCriacaoAsync(Consulta consulta)
    {
        if (consulta.Formato != FormatoConsulta.Online || consulta.Id <= 0)
            return;
        if (!string.IsNullOrWhiteSpace(consulta.VideoRoomName) &&
            consulta.VideoRoomName.StartsWith($"clinicapsi-{consulta.Id}-", StringComparison.Ordinal))
            return;

        var (roomName, roomUrl) = await GerarSalaAsync(consulta.Id);
        consulta.VideoRoomName = roomName;
        consulta.VideoRoomUrl = roomUrl;
        consulta.DataAtualizacao = DateTime.Now;
        await _context.SaveChangesAsync();
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
