using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClinicaPsi.Application.Services;

public class NotificacaoService
{
    private readonly AppDbContext _context;
    private readonly ILogger<NotificacaoService> _logger;

    public NotificacaoService(AppDbContext context, ILogger<NotificacaoService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Envia lembrete de consulta 24h antes
    /// </summary>
    public async Task<bool> EnviarLembreteConsultaAsync(int consultaId)
    {
        try
        {
            var consulta = await _context.Consultas
                .Include(c => c.Paciente)
                .Include(c => c.Psicologo)
                .FirstOrDefaultAsync(c => c.Id == consultaId);

            if (consulta == null || consulta.Paciente == null || consulta.Psicologo == null)
                return false;

            var mensagem = $@"
🔔 LEMBRETE DE CONSULTA

Olá {consulta.Paciente.Nome}!

Você tem uma consulta agendada:

📅 Data: {consulta.DataHorario:dd/MM/yyyy}
🕐 Horário: {consulta.DataHorario:HH:mm}
👨‍⚕️ Psicólogo(a): {consulta.Psicologo.Nome}
💰 Valor: R$ {consulta.Valor:F2}

📍 Endereço: Rua Orlando Ferreira Neto, 39 - Jd Itapoã, Londrina - PR

Por favor, confirme sua presença respondendo a esta mensagem.

Em caso de imprevistos, entre em contato com antecedência.

Atenciosamente,
PsiiAnaSantos - Clínica de Psicologia
📞 (42) 99936-9724
";

            // TODO: Integrar com APIs reais
            // Por enquanto, apenas simula o envio
            
            // Enviar por WhatsApp (prioridade)
            if (!string.IsNullOrEmpty(consulta.Paciente.Telefone))
            {
                _logger.LogInformation($"[WHATSAPP] Enviando para {consulta.Paciente.Telefone}: {mensagem}");
                // await EnviarWhatsAppAsync(consulta.Paciente.Telefone, mensagem);
            }

            // Enviar por Email (backup)
            if (!string.IsNullOrEmpty(consulta.Paciente.Email))
            {
                _logger.LogInformation($"[EMAIL] Enviando para {consulta.Paciente.Email}: {mensagem}");
                // await EnviarEmailAsync(consulta.Paciente.Email, "Lembrete de Consulta", mensagem);
            }

            // Marcar como notificação enviada
            consulta.NotificacaoEnviada = true;
            await _context.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Erro ao enviar lembrete da consulta {consultaId}");
            return false;
        }
    }

    /// <summary>
    /// Envia confirmação de agendamento
    /// </summary>
    public async Task<bool> EnviarConfirmacaoAgendamentoAsync(int consultaId)
    {
        try
        {
            var consulta = await _context.Consultas
                .Include(c => c.Paciente)
                .Include(c => c.Psicologo)
                .FirstOrDefaultAsync(c => c.Id == consultaId);

            if (consulta == null || consulta.Paciente == null || consulta.Psicologo == null)
                return false;

            var mensagem = $@"
✅ CONSULTA AGENDADA COM SUCESSO!

Olá {consulta.Paciente.Nome}!

Sua consulta foi confirmada:

📅 Data: {consulta.DataHorario:dd/MM/yyyy}
🕐 Horário: {consulta.DataHorario:HH:mm}
👨‍⚕️ Psicólogo(a): {consulta.Psicologo.Nome}
💰 Valor: R$ {consulta.Valor:F2}

📍 Local:
Rua Orlando Ferreira Neto, 39 - Jd Itapoã
Londrina - PR, 86043-470

💡 IMPORTANTE:
- Chegue com 10 minutos de antecedência
- Em caso de atraso, avise com antecedência
- Cancelamentos devem ser feitos com 24h de antecedência

🎁 SISTEMA PSICOPONTOS:
Você ganha 1 ponto a cada consulta realizada!
Acumule 10 pontos e ganhe 1 consulta gratuita! 🎉

Atenciosamente,
PsiiAnaSantos - Clínica de Psicologia
📞 (42) 99936-9724
📧 psiianasantos@psiianasantos.com.br
";

            // Enviar notificações
            if (!string.IsNullOrEmpty(consulta.Paciente.Telefone))
            {
                _logger.LogInformation($"[WHATSAPP] Confirmação enviada para {consulta.Paciente.Telefone}");
            }

            if (!string.IsNullOrEmpty(consulta.Paciente.Email))
            {
                _logger.LogInformation($"[EMAIL] Confirmação enviada para {consulta.Paciente.Email}");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Erro ao enviar confirmação da consulta {consultaId}");
            return false;
        }
    }

    /// <summary>
    /// Envia notificação de cancelamento
    /// </summary>
    public async Task<bool> EnviarNotificacaoCancelamentoAsync(int consultaId, string motivo)
    {
        try
        {
            var consulta = await _context.Consultas
                .Include(c => c.Paciente)
                .Include(c => c.Psicologo)
                .FirstOrDefaultAsync(c => c.Id == consultaId);

            if (consulta == null || consulta.Paciente == null)
                return false;

            var mensagem = $@"
❌ CONSULTA CANCELADA

Olá {consulta.Paciente.Nome},

Informamos que sua consulta foi cancelada:

📅 Data: {consulta.DataHorario:dd/MM/yyyy}
🕐 Horário: {consulta.DataHorario:HH:mm}
👨‍⚕️ Psicólogo(a): {consulta.Psicologo?.Nome}

Motivo: {motivo}

Para reagendar, entre em contato:
📞 (42) 99936-9724
📧 psiianasantos@psiianasantos.com.br

Atenciosamente,
PsiiAnaSantos
";

            if (!string.IsNullOrEmpty(consulta.Paciente.Telefone))
            {
                _logger.LogInformation($"[WHATSAPP] Cancelamento notificado para {consulta.Paciente.Telefone}");
            }

            if (!string.IsNullOrEmpty(consulta.Paciente.Email))
            {
                _logger.LogInformation($"[EMAIL] Cancelamento notificado para {consulta.Paciente.Email}");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Erro ao enviar notificação de cancelamento {consultaId}");
            return false;
        }
    }

    /// <summary>
    /// Verifica consultas que precisam de lembrete (24h antes)
    /// </summary>
    public async Task<List<Consulta>> ObterConsultasParaLembreteAsync()
    {
        var amanha = DateTime.Now.AddHours(24);
        var limite = DateTime.Now.AddHours(25);

        return await _context.Consultas
            .Include(c => c.Paciente)
            .Include(c => c.Psicologo)
            .Where(c => c.DataHorario >= amanha && 
                       c.DataHorario <= limite &&
                       c.Status == StatusConsulta.Agendada &&
                       !c.NotificacaoEnviada)
            .ToListAsync();
    }

    /// <summary>
    /// Processa envio de lembretes em lote
    /// </summary>
    public async Task<int> ProcessarLembretesAsync()
    {
        var consultas = await ObterConsultasParaLembreteAsync();
        var enviados = 0;

        foreach (var consulta in consultas)
        {
            if (await EnviarLembreteConsultaAsync(consulta.Id))
            {
                enviados++;
            }
        }

        _logger.LogInformation($"Processados {enviados} lembretes de {consultas.Count} consultas");
        return enviados;
    }

    // Métodos privados para integração com APIs (TODO: implementar)
    
    private async Task<bool> EnviarWhatsAppAsync(string telefone, string mensagem)
    {
        // TODO: Integrar com WhatsApp Business API ou Twilio
        await Task.Delay(100); // Simula envio
        return true;
    }

    private async Task<bool> EnviarEmailAsync(string email, string assunto, string mensagem)
    {
        // TODO: Integrar com SMTP ou SendGrid
        await Task.Delay(100); // Simula envio
        return true;
    }

    private async Task<bool> EnviarSMSAsync(string telefone, string mensagem)
    {
        // TODO: Integrar com Twilio ou similar
        await Task.Delay(100); // Simula envio
        return true;
    }
}
