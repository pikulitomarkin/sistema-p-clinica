using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;

namespace ClinicaPsi.Application.Services;

public class WhatsAppBotService
{
    private readonly WhatsAppService _wa;
    private readonly OpenAIService _openAi;
    private readonly ConsultaService _consultaService;
    private readonly PacienteService _pacienteService;
    private readonly PsicologoService _psicologoService;
    private readonly ILogger<WhatsAppBotService> _logger;

    public WhatsAppBotService(
        WhatsAppService wa,
        OpenAIService openAi,
        ConsultaService consultaService,
        PacienteService pacienteService,
        PsicologoService psicologoService,
        ILogger<WhatsAppBotService> logger)
    {
        _wa = wa;
        _openAi = openAi;
        _consultaService = consultaService;
        _pacienteService = pacienteService;
        _psicologoService = psicologoService;
        _logger = logger;
    }

    /// <summary>
    /// Processa mensagem recebida do usuário via WhatsApp.
    /// Reconhece intents básicas: agendar, remarcar, cancelar. Se não reconhecer, encaminha para OpenAI.
    /// </summary>
    public async Task ProcessIncomingMessageAsync(string fromNumber, string texto)
    {
        try
        {
            var normalized = texto?.Trim().ToLowerInvariant() ?? string.Empty;

            if (IsIntent(normalized, new[] { "agendar", "marcar" }))
            {
                await HandleAgendarAsync(fromNumber, texto);
                return;
            }

            if (IsIntent(normalized, new[] { "remarcar", "reagendar" }))
            {
                await HandleRemarcarAsync(fromNumber, texto);
                return;
            }

            if (IsIntent(normalized, new[] { "cancelar", "desmarcar" }))
            {
                await HandleCancelarAsync(fromNumber, texto);
                return;
            }

            // Default: usar OpenAI para resposta conversacional
            var resposta = await _openAi.GetChatResponseAsync(normalized, "Você é um assistente virtual que responde em português. Seja breve e educado. Se o usuário pedir para agendar, peça data e horário.");
            await _wa.EnviarMensagemTextoAsync(fromNumber, resposta);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro processando mensagem WhatsApp");
            await _wa.EnviarMensagemTextoAsync(fromNumber, "Desculpe, ocorreu um erro ao processar sua mensagem. Tente novamente mais tarde.");
        }
    }

    private bool IsIntent(string text, string[] keywords)
    {
        return keywords.Any(k => text.Contains(k));
    }

    private async Task HandleAgendarAsync(string fromNumber, string? texto)
    {
        // Estratégia simples: pedir data/hora se não houver na mensagem
        var safeTexto = texto ?? string.Empty;
        var dataHora = ExtractDateTime(safeTexto);
        if (dataHora == null)
        {
            await _wa.EnviarMensagemTextoAsync(fromNumber, "Posso agendar sua consulta — por favor informe data e hora no formato DD/MM/AAAA HH:MM ou diga 'amanhã às 15:00'.");
            return;
        }
        // Localizar ou criar paciente por telefone (simplificado)
        var paciente = await _pacienteService.GetByPhoneAsync(fromNumber) ?? await _pacienteService.CreateOrGetByPhoneAsync(fromNumber);

        // Obter psicólogo padrão (primeiro ativo)
        var psicologos = await _psicologoService.GetAllAsync();
        var psicologo = psicologos.FirstOrDefault();
        if (psicologo == null)
        {
            await _wa.EnviarMensagemTextoAsync(fromNumber, "No momento não há psicólogos disponíveis. Por favor tente novamente mais tarde.");
            return;
        }

        var consulta = new ClinicaPsi.Shared.Models.Consulta
        {
            PacienteId = paciente.Id,
            PsicologoId = psicologo.Id,
            DataHorario = dataHora.Value,
            DuracaoMinutos = 50
        };

        var agendada = await _consultaService.AgendarAsync(consulta);
        await _wa.EnviarConfirmacaoAgendamentoAsync(fromNumber, paciente.Nome ?? "Paciente", agendada.DataHorario, "Atendimento online");
    }

    private async Task HandleRemarcarAsync(string fromNumber, string? texto)
    {
        // Exemplo de fluxo:
        // 1) Extrair ID da consulta da mensagem
        // 2) Extrair nova data/hora
        // 3) Validar que a consulta pertence ao paciente (por telefone)
        // 4) Agendar nova consulta e cancelar a antiga

        var safeTexto = texto ?? string.Empty;
        var id = ExtractDigits(safeTexto);
        var novaData = ExtractDateTime(safeTexto);

        if (id == null)
        {
            await _wa.EnviarMensagemTextoAsync(fromNumber, "Por favor informe o ID da consulta que deseja remarcar seguido da nova data/horário (ex: 'remarcar 123 para 10/11/2025 15:00').");
            return;
        }

        if (novaData == null)
        {
            await _wa.EnviarMensagemTextoAsync(fromNumber, "Por favor informe a nova data e horário desejado no formato DD/MM/AAAA HH:MM.");
            return;
        }

        var consulta = await _consultaService.GetByIdAsync(id.Value);
        if (consulta == null)
        {
            await _wa.EnviarMensagemTextoAsync(fromNumber, "Não encontrei uma consulta com esse ID.");
            return;
        }

        // Verificar se a consulta pertence ao paciente que está solicitando
        var paciente = await _pacienteService.GetByPhoneAsync(fromNumber) ?? await _pacienteService.CreateOrGetByPhoneAsync(fromNumber);
        if (consulta.PacienteId != paciente.Id)
        {
            await _wa.EnviarMensagemTextoAsync(fromNumber, "Essa consulta não pertence a este contato. Se for sua, verifique o ID ou entre em contato com a clínica.");
            return;
        }

        // Agendar nova consulta (mantendo mesmo psicólogo, duração padrão)
        var novaConsulta = new ClinicaPsi.Shared.Models.Consulta
        {
            PacienteId = paciente.Id,
            PsicologoId = consulta.PsicologoId,
            DataHorario = novaData.Value,
            DuracaoMinutos = consulta.DuracaoMinutos,
            Tipo = consulta.Tipo
        };

        var agendada = await _consultaService.AgendarAsync(novaConsulta);
        var cancelada = await _consultaService.CancelarAsync(consulta.Id, "Remarcada via WhatsApp - nova consulta ID: " + agendada.Id);

        if (agendada != null && cancelada)
        {
            await _wa.EnviarMensagemTextoAsync(fromNumber, $"Sua consulta foi reagendada para {agendada.DataHorario:dd/MM/yyyy HH:mm}. ID nova consulta: {agendada.Id}.");
        }
        else
        {
            await _wa.EnviarMensagemTextoAsync(fromNumber, "Ocorreu um problema ao remarcar sua consulta. Por favor tente novamente ou contate a clínica.");
        }
    }

    private async Task HandleCancelarAsync(string fromNumber, string? texto)
    {
        // Procurar por ID na mensagem
        var id = ExtractDigits(texto ?? string.Empty);
        if (id == null)
        {
            await _wa.EnviarMensagemTextoAsync(fromNumber, "Por favor informe o ID da consulta que deseja cancelar ou a data/horário da mesma.");
            return;
        }
        var sucesso = await _consultaService.CancelarAsync(id.Value, "Cancelado via WhatsApp");
        if (sucesso)
            await _wa.EnviarMensagemTextoAsync(fromNumber, "Consulta cancelada com sucesso. Se precisar, posso ajudar a reagendar.");
        else
            await _wa.EnviarMensagemTextoAsync(fromNumber, "Não encontrei essa consulta. Verifique o ID e tente novamente.");
    }

    private DateTime? ExtractDateTime(string texto)
    {
        // Implementação simples: tenta extrair dd/mm/yyyy hh:mm
        var m = Regex.Match(texto, @"(\d{1,2})[/-](\d{1,2})[/-](\d{2,4})(?:\s+(\d{1,2}):(\d{2}))?");
        if (m.Success)
        {
            try
            {
                var d = int.Parse(m.Groups[1].Value);
                var mo = int.Parse(m.Groups[2].Value);
                var y = int.Parse(m.Groups[3].Value);
                if (y < 100) y += 2000;
                var h = m.Groups[4].Success ? int.Parse(m.Groups[4].Value) : 9;
                var mi = m.Groups[5].Success ? int.Parse(m.Groups[5].Value) : 0;
                return new DateTime(y, mo, d, h, mi, 0);
            }
            catch { return null; }
        }

        // Reconhecer 'amanhã às 15:00'
        m = Regex.Match(texto, @"amanh[aã](?:\s+às?)?\s*(\d{1,2}):(\d{2})");
        if (m.Success)
        {
            var h = int.Parse(m.Groups[1].Value);
            var mi = int.Parse(m.Groups[2].Value);
            return DateTime.Today.AddDays(1).AddHours(h).AddMinutes(mi);
        }

        return null;
    }

    private int? ExtractDigits(string texto)
    {
        var m = Regex.Match(texto, "\\d+");
        if (m.Success) return int.Parse(m.Value);
        return null;
    }
}
