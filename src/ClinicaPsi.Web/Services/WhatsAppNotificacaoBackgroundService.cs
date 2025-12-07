using ClinicaPsi.Application.Services;

namespace ClinicaPsi.Web.Services;

/// <summary>
/// Serviço de background para enviar notificações automáticas de consultas via WhatsApp
/// Executa diariamente às 9h da manhã para enviar lembretes de consultas que acontecerão nas próximas 24h
/// </summary>
public class WhatsAppNotificacaoBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WhatsAppNotificacaoBackgroundService> _logger;
    private readonly TimeSpan _horarioExecucao = new TimeSpan(9, 0, 0); // 9:00 AM

    public WhatsAppNotificacaoBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<WhatsAppNotificacaoBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🤖 WhatsApp Notificação Background Service iniciado");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.Now;
                var proximaExecucao = CalcularProximaExecucao(now);
                var delay = proximaExecucao - now;

                if (delay.TotalMilliseconds > 0)
                {
                    _logger.LogInformation(
                        "⏰ Próxima execução de notificações WhatsApp agendada para: {ProximaExecucao}",
                        proximaExecucao.ToString("dd/MM/yyyy HH:mm:ss"));

                    await Task.Delay(delay, stoppingToken);
                }

                // Executar envio de notificações
                await EnviarNotificacoesAsync(stoppingToken);

            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("WhatsApp Notificação Background Service foi cancelado");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro no WhatsApp Notificação Background Service");
                
                // Aguardar 1 hora antes de tentar novamente em caso de erro
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }

    private DateTime CalcularProximaExecucao(DateTime agora)
    {
        var proximaExecucao = agora.Date + _horarioExecucao;

        // Se já passou das 9h hoje, agendar para amanhã às 9h
        if (agora >= proximaExecucao)
        {
            proximaExecucao = proximaExecucao.AddDays(1);
        }

        return proximaExecucao;
    }

    private async Task EnviarNotificacoesAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("📤 Iniciando envio de notificações WhatsApp de consultas...");

            using var scope = _serviceProvider.CreateScope();
            var notificationService = scope.ServiceProvider.GetRequiredService<WhatsAppNotificationService>();

            await notificationService.EnviarNotificacoesConsultasAmanha();

            _logger.LogInformation("✅ Notificações WhatsApp enviadas com sucesso!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao enviar notificações WhatsApp");
            throw;
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🛑 WhatsApp Notificação Background Service está parando...");
        return base.StopAsync(cancellationToken);
    }
}
