using ClinicaPsi.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClinicaPsi.Web.Services;

public class NotificacaoBackgroundService : BackgroundService
{
    private readonly ILogger<NotificacaoBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _intervalo = TimeSpan.FromHours(1); // Verifica a cada 1 hora

    public NotificacaoBackgroundService(
        ILogger<NotificacaoBackgroundService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Serviço de Notificações iniciado");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessarNotificacoesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar notificações");
            }

            await Task.Delay(_intervalo, stoppingToken);
        }

        _logger.LogInformation("Serviço de Notificações encerrado");
    }

    private async Task ProcessarNotificacoesAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var notificacaoService = scope.ServiceProvider.GetRequiredService<NotificacaoService>();

        _logger.LogInformation("Processando lembretes de consultas...");
        var enviados = await notificacaoService.ProcessarLembretesAsync();
        
        if (enviados > 0)
        {
            _logger.LogInformation($"{enviados} lembretes enviados com sucesso");
        }
    }
}
