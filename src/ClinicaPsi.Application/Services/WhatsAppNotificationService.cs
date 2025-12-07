using ClinicaPsi.Infrastructure.Data;
using ClinicaPsi.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ClinicaPsi.Application.Services;

/// <summary>
/// Serviço responsável por enviar notificações automáticas de consultas via WhatsApp
/// </summary>
public class WhatsAppNotificationService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly WhatsAppWebService _whatsAppService;
    private readonly ILogger<WhatsAppNotificationService> _logger;
    private readonly IConfiguration _configuration;

    public WhatsAppNotificationService(
        IDbContextFactory<AppDbContext> contextFactory,
        WhatsAppWebService whatsAppService,
        ILogger<WhatsAppNotificationService> logger,
        IConfiguration configuration)
    {
        _contextFactory = contextFactory;
        _whatsAppService = whatsAppService;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Envia notificações para todas as consultas que acontecerão em 24 horas
    /// </summary>
    public async Task EnviarNotificacoesConsultasAmanha()
    {
        try
        {
            // Obter configurações
            var botAtivo = _configuration.GetValue<bool>("WhatsApp:BotAtivo", true);
            if (!botAtivo)
            {
                _logger.LogInformation("Bot desativado. Notificações não serão enviadas.");
                return;
            }

            // Calcular janela de tempo (24h a partir de agora)
            var agora = DateTime.Now;
            var inicioJanela = agora.AddHours(23);
            var fimJanela = agora.AddHours(25);

            // Criar contexto usando factory
            using var context = await _contextFactory.CreateDbContextAsync();
            
            // Buscar consultas agendadas para amanhã
            var consultas = await context.Consultas
                .Include(c => c.Paciente)
                .Include(c => c.Psicologo)
                .Where(c => c.Status == StatusConsulta.Agendada || c.Status == StatusConsulta.Confirmada)
                .Where(c => c.DataHorario >= inicioJanela && c.DataHorario <= fimJanela)
                .ToListAsync();

            _logger.LogInformation("Encontradas {Count} consultas para notificar", consultas.Count);

            foreach (var consulta in consultas)
            {
                await EnviarNotificacaoConsulta(consulta);
                
                // Aguardar 2 segundos entre mensagens para não sobrecarregar
                await Task.Delay(2000);
            }

            _logger.LogInformation("✅ Notificações enviadas com sucesso!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar notificações de consultas");
        }
    }

    /// <summary>
    /// Envia notificação individual de consulta
    /// </summary>
    private async Task EnviarNotificacaoConsulta(Consulta consulta)
    {
        try
        {
            if (consulta.Paciente == null || consulta.Psicologo == null)
            {
                _logger.LogWarning("Consulta {Id} sem paciente ou psicólogo", consulta.Id);
                return;
            }

            var telefone = consulta.Paciente.Telefone;
            if (string.IsNullOrEmpty(telefone))
            {
                _logger.LogWarning("Paciente {Nome} sem telefone cadastrado", consulta.Paciente.Nome);
                return;
            }

            // Limpar telefone (remover caracteres especiais)
            telefone = LimparTelefone(telefone);

            // Montar mensagem
            var dataConsulta = consulta.DataHorario.ToString("dd/MM/yyyy");
            var horaConsulta = consulta.DataHorario.ToString("HH:mm");
            var diaSemana = ObterDiaSemana(consulta.DataHorario.DayOfWeek);
            
            var siteUrl = _configuration["WhatsApp:SiteUrl"] ?? "https://seu-site.com";

            var mensagem = $@"🏥 *Lembrete de Consulta*

Olá *{consulta.Paciente.Nome}*!

📅 Você tem uma consulta agendada para:
• *{diaSemana}, {dataConsulta}*
• *Horário:* {horaConsulta}
• *Psicóloga:* {consulta.Psicologo.Nome}

⏰ *Importante:* Chegue com 10 minutos de antecedência.

---

*Precisa reagendar ou cancelar?*

Acesse nosso site: {siteUrl}
1️⃣ Faça login com seu e-mail
2️⃣ Vá em ""Minhas Consultas""
3️⃣ Clique em ""Reagendar"" ou ""Cancelar""

💡 *Dica:* Reagendamentos devem ser feitos com pelo menos 24h de antecedência.

---

Se tiver alguma dúvida, responda esta mensagem que um atendente entrará em contato! 😊";

            var enviado = await _whatsAppService.EnviarMensagemAsync(telefone, mensagem);

            if (enviado)
            {
                _logger.LogInformation("✅ Notificação enviada para {Paciente} - Consulta {Data}", 
                    consulta.Paciente.Nome, consulta.DataHorario);
            }
            else
            {
                _logger.LogWarning("❌ Falha ao enviar notificação para {Paciente}", consulta.Paciente.Nome);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar notificação da consulta {Id}", consulta.Id);
        }
    }

    /// <summary>
    /// Processa mensagem recebida do paciente
    /// </summary>
    public async Task ProcessarMensagemRecebida(string telefone, string mensagem)
    {
        try
        {
            _logger.LogInformation("📨 Mensagem recebida de {Telefone}: {Mensagem}", telefone, mensagem);

            // Limpar telefone
            var telefoneLimpo = LimparTelefone(telefone);

            // Criar contexto usando factory
            using var context = await _contextFactory.CreateDbContextAsync();
            
            // Verificar se é paciente cadastrado
            var paciente = await context.Pacientes
                .FirstOrDefaultAsync(p => p.Telefone.Contains(telefoneLimpo.Substring(telefoneLimpo.Length - 8)));

            var mensagemLower = mensagem.ToLower().Trim();

            // Resposta baseada em palavras-chave
            string resposta = "";

            // Saudações
            if (EhSaudacao(mensagemLower))
            {
                resposta = GerarMensagemBoasVindas(paciente?.Nome);
            }
            // Reagendar ou Cancelar
            else if (mensagemLower.Contains("reagendar") || mensagemLower.Contains("remarcar") || 
                     mensagemLower.Contains("cancelar") || mensagemLower.Contains("desmarcar"))
            {
                resposta = GerarMensagemReagendarCancelar();
            }
            // Horários
            else if (mensagemLower.Contains("horario") || mensagemLower.Contains("horário") || 
                     mensagemLower.Contains("disponivel") || mensagemLower.Contains("disponível"))
            {
                resposta = GerarMensagemHorarios();
            }
            // Localização
            else if (mensagemLower.Contains("endereço") || mensagemLower.Contains("endereco") || 
                     mensagemLower.Contains("local") || mensagemLower.Contains("fica") || 
                     mensagemLower.Contains("onde"))
            {
                resposta = GerarMensagemLocalizacao();
            }
            // Valores
            else if (mensagemLower.Contains("valor") || mensagemLower.Contains("preço") || 
                     mensagemLower.Contains("preco") || mensagemLower.Contains("quanto custa"))
            {
                resposta = GerarMensagemValores();
            }
            // Confirmação de consulta
            else if (mensagemLower.Contains("confirmar") || mensagemLower.Contains("confirmo"))
            {
                resposta = "✅ Obrigada pela confirmação! Sua consulta está confirmada.\n\nNos vemos em breve! 😊";
            }
            // Não entendeu - encaminhar para atendente
            else
            {
                resposta = await EncaminharParaAtendente(telefone, mensagem, paciente?.Nome);
            }

            // Enviar resposta
            if (!string.IsNullOrEmpty(resposta))
            {
                await _whatsAppService.EnviarMensagemAsync(telefone, resposta);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar mensagem recebida");
        }
    }

    private bool EhSaudacao(string mensagem)
    {
        var saudacoes = new[] { "oi", "olá", "ola", "bom dia", "boa tarde", "boa noite", "alo", "alô" };
        return saudacoes.Any(s => mensagem.Contains(s));
    }

    private string GerarMensagemBoasVindas(string? nomePaciente)
    {
        var nome = !string.IsNullOrEmpty(nomePaciente) ? nomePaciente : "visitante";
        var siteUrl = _configuration["WhatsApp:SiteUrl"] ?? "https://seu-site.com";

        return $@"🏥 Olá *{nome}*! Bem-vindo(a) à Psicóloga Ana Santos! 😊

Como posso ajudar você hoje?

📋 *Menu de Opções:*

1️⃣ Reagendar ou Cancelar consulta
2️⃣ Ver horários disponíveis
3️⃣ Endereço e localização
4️⃣ Valores das consultas
5️⃣ Falar com atendente

Digite o número da opção ou me envie sua dúvida diretamente!

💻 *Site:* {siteUrl}";
    }

    private string GerarMensagemReagendarCancelar()
    {
        var siteUrl = _configuration["WhatsApp:SiteUrl"] ?? "https://seu-site.com";

        return $@"📅 *Reagendar ou Cancelar Consulta*

Para reagendar ou cancelar sua consulta, siga os passos:

*Passo a Passo:*

1️⃣ Acesse: {siteUrl}
2️⃣ Faça login com seu e-mail e senha
3️⃣ Clique em ""Minhas Consultas"" no menu
4️⃣ Localize sua consulta
5️⃣ Clique em ""Reagendar"" ou ""Cancelar""

⚠️ *Importante:*
• Reagendamentos: mínimo 24h de antecedência
• Cancelamentos: até 24h antes da consulta
• Após esse prazo, entre em contato conosco

💡 *Dificuldade para acessar?*
Digite ""atendente"" para falar com nossa equipe!";
    }

    private string GerarMensagemHorarios()
    {
        var siteUrl = _configuration["WhatsApp:SiteUrl"] ?? "https://seu-site.com";

        return $@"⏰ *Horários de Atendimento*

A Psicóloga Ana Santos atende:

📅 *Segunda a Sexta:*
• Manhã: 08h às 12h
• Tarde: 14h às 18h

📅 *Sábado:*
• Manhã: 08h às 12h

❌ *Domingo:* Não atendemos

---

*Para ver os horários disponíveis e agendar:*

Acesse: {siteUrl}
1️⃣ Faça login
2️⃣ Clique em ""Agendar Consulta""
3️⃣ Escolha o melhor horário

Precisa de ajuda? Digite ""atendente""!";
    }

    private string GerarMensagemLocalizacao()
    {
        var endereco = _configuration["Clinica:Endereco"] ?? "Londrina - PR";
        
        return $@"📍 *Localização*

Atendemos em:
*{endereco}*

🚗 *Como chegar:*
• Estacionamento disponível
• Próximo a pontos de ônibus

🏢 *Referências:*
• Enviaremos o endereço completo após confirmação do agendamento

💬 Para mais informações, digite ""atendente""!";
    }

    private string GerarMensagemValores()
    {
        return @"💰 *Valores das Consultas*

Os valores são informados durante o agendamento no site.

📋 *Formas de Pagamento:*
• Dinheiro
• Cartão de débito/crédito
• PIX
• Transferência bancária

💡 Trabalhamos com convênios e particulares.

Para mais informações sobre valores e convênios, digite ""atendente""!";
    }

    private async Task<string> EncaminharParaAtendente(string telefone, string mensagem, string? nomePaciente)
    {
        try
        {
            // Número do atendente (configurável)
            var numeroAtendente = _configuration["WhatsApp:NumeroAtendente"];
            
            if (!string.IsNullOrEmpty(numeroAtendente))
            {
                var mensagemAtendente = $@"🔔 *Nova Mensagem de Paciente*

👤 *Paciente:* {nomePaciente ?? "Não identificado"}
📱 *Telefone:* {telefone}

💬 *Mensagem:*
{mensagem}

---
⏰ {DateTime.Now:dd/MM/yyyy HH:mm}";

                await _whatsAppService.EnviarMensagemAsync(numeroAtendente, mensagemAtendente);
                _logger.LogInformation("📨 Mensagem encaminhada para atendente");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao encaminhar mensagem para atendente");
        }

        return @"👤 *Mensagem Encaminhada*

Sua dúvida foi encaminhada para nossa equipe!

Um atendente entrará em contato com você em breve. 😊

⏰ *Horário de atendimento:*
Segunda a Sexta: 8h às 18h
Sábado: 8h às 12h";
    }

    private string LimparTelefone(string telefone)
    {
        if (string.IsNullOrEmpty(telefone))
            return "";

        // Remover tudo exceto números
        var limpo = new string(telefone.Where(char.IsDigit).ToArray());

        // Garantir que tenha código do país
        if (!limpo.StartsWith("55"))
            limpo = "55" + limpo;

        return limpo;
    }

    private string ObterDiaSemana(DayOfWeek dia)
    {
        return dia switch
        {
            DayOfWeek.Sunday => "Domingo",
            DayOfWeek.Monday => "Segunda-feira",
            DayOfWeek.Tuesday => "Terça-feira",
            DayOfWeek.Wednesday => "Quarta-feira",
            DayOfWeek.Thursday => "Quinta-feira",
            DayOfWeek.Friday => "Sexta-feira",
            DayOfWeek.Saturday => "Sábado",
            _ => ""
        };
    }
}
