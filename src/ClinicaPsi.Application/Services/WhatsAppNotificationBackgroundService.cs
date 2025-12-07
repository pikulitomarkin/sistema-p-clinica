using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClinicaPsi.Application.Services;

/// <summary>
/// Serviço em background que executa o envio de notificações diariamente
/// </summary>
public class WhatsAppNotificationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WhatsAppNotificationBackgroundService> _logger;
    private readonly TimeSpan _intervalo = TimeSpan.FromHours(24); // Executar a cada 24 horas
    private readonly TimeSpan _horarioExecucao = new TimeSpan(9, 0, 0); // 09:00 da manhã

    public WhatsAppNotificationBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<WhatsAppNotificationBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🤖 WhatsApp Notification Background Service iniciado");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Calcular próxima execução
                var proximaExecucao = CalcularProximaExecucao();
                var tempoAteProximaExecucao = proximaExecucao - DateTime.Now;

                if (tempoAteProximaExecucao.TotalMilliseconds > 0)
                {
                    _logger.LogInformation("⏰ Próximo envio de notificações: {Data} ({Tempo})",
                        proximaExecucao.ToString("dd/MM/yyyy HH:mm"),
                        FormatarTempoEspera(tempoAteProximaExecucao));

                    await Task.Delay(tempoAteProximaExecucao, stoppingToken);
                }

                // Executar envio de notificações
                await EnviarNotificacoes();

                // Aguardar até a próxima execução
                await Task.Delay(_intervalo, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("🛑 WhatsApp Notification Background Service cancelado");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro no Background Service de notificações");
                
                // Aguardar 1 hora antes de tentar novamente em caso de erro
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }

    private async Task EnviarNotificacoes()
    {
        try
        {
            _logger.LogInformation("📨 Iniciando envio de notificações...");

            // Criar um novo scope para resolver o serviço
            using var scope = _serviceProvider.CreateScope();
            var notificationService = scope.ServiceProvider.GetRequiredService<WhatsAppNotificationService>();

            await notificationService.EnviarNotificacoesConsultasAmanha();

            _logger.LogInformation("✅ Envio de notificações concluído com sucesso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao enviar notificações");
            throw;
        }
    }

    private DateTime CalcularProximaExecucao()
    {
        var agora = DateTime.Now;
        var proximaExecucao = agora.Date.Add(_horarioExecucao);

        // Se já passou das 9h hoje, agendar para amanhã
        if (agora >= proximaExecucao)
        {
            proximaExecucao = proximaExecucao.AddDays(1);
        }

        return proximaExecucao;
    }

    private string FormatarTempoEspera(TimeSpan tempo)
    {
        if (tempo.TotalHours >= 1)
        {
            return $"{tempo.Hours}h {tempo.Minutes}min";
        }
        else
        {
            return $"{tempo.Minutes}min";
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🛑 Parando WhatsApp Notification Background Service");
        return base.StopAsync(cancellationToken);
    }
}
