# Sistema de Notificações Automáticas WhatsApp - ClinicaPsi

## 📋 Visão Geral

Sistema completo de notificações automáticas via WhatsApp com bot inteligente para atendimento automatizado.

### ✅ Status da Implementação

**COMPLETO** - Todas as funcionalidades implementadas e testadas:

- ✅ Bot WhatsApp conectado (Baileys)
- ✅ Notificações automáticas 24h antes das consultas
- ✅ Bot inteligente com respostas automatizadas
- ✅ Encaminhamento para atendente humano
- ✅ Background service rodando diariamente
- ✅ Webhook para receber mensagens
- ✅ Formatação automática de telefone (55 + DDD + número)

---

## 🏗️ Arquitetura do Sistema

### Componentes Principais

```
┌─────────────────────────────────────────────────────────────┐
│                    SISTEMA DE NOTIFICAÇÕES                    │
└─────────────────────────────────────────────────────────────┘

┌──────────────────────┐      ┌──────────────────────┐
│   ASP.NET Core 9.0   │◄────►│  WhatsApp Bot (Node) │
│  ClinicaPsi.Web      │      │   server-baileys.js  │
└──────────────────────┘      └──────────────────────┘
         │                              │
         │                              │
    ┌────▼────┐                   ┌────▼────┐
    │PostgreSQL│                   │PostgreSQL│
    │ Railway  │                   │ Railway  │
    └──────────┘                   └──────────┘
         │                              │
         └──────────────┬───────────────┘
                        │
                   ┌────▼────┐
                   │WhatsApp │
                   │  API    │
                   └─────────┘
```

---

## 📦 Componentes Implementados

### 1. **WhatsAppNotificationService.cs**
**Localização**: `src/ClinicaPsi.Application/Services/`

**Responsabilidades**:
- ✅ Enviar notificações de consultas 24h antes
- ✅ Formatar telefone com código do país (55)
- ✅ Processar mensagens recebidas dos pacientes
- ✅ Responder automaticamente com base em palavras-chave
- ✅ Encaminhar dúvidas complexas para atendente humano

**Métodos Principais**:

```csharp
// Enviar notificações das consultas de amanhã
public async Task EnviarNotificacoesConsultasAmanha()

// Processar mensagem recebida do paciente
public async Task ProcessarMensagemRecebida(string telefone, string mensagem)

// Limpar e formatar telefone (adicionar 55)
private string LimparTelefone(string telefone)
```

**Respostas Inteligentes**:
- 🖐️ **Saudações**: "Olá", "Oi", "Bom dia" → Menu de opções
- 📅 **Reagendar/Cancelar** → Instruções + link do site
- ⏰ **Horários** → Horários de atendimento
- 📍 **Localização** → Endereço da clínica
- 💰 **Valores** → Informações sobre valores e convênios
- ❓ **Outras dúvidas** → Encaminha para atendente humano

---

### 2. **WhatsAppNotificationBackgroundService.cs**
**Localização**: `src/ClinicaPsi.Application/Services/`

**Responsabilidades**:
- ✅ Executar diariamente às 09:00
- ✅ Chamar `EnviarNotificacoesConsultasAmanha()`
- ✅ Aguardar 24h e repetir
- ✅ Tratamento de erros com retry

**Configuração**:
```csharp
private readonly TimeSpan _intervalo = TimeSpan.FromHours(24); // 24h
private readonly TimeSpan _horarioExecucao = new TimeSpan(9, 0, 0); // 09:00
```

**Log de Execução**:
```
🤖 WhatsApp Notification Background Service iniciado
⏰ Próximo envio de notificações: 11/01/2025 09:00 (23h 45min)
📨 Iniciando envio de notificações...
✅ Envio de notificações concluído com sucesso
```

---

### 3. **WhatsAppWebhookController.cs**
**Localização**: `src/ClinicaPsi.Web/Controllers/`

**Endpoints**:

#### 📨 Receber Mensagem
```
POST /webhook/whatsapp
```

**Request Body**:
```json
{
  "sessionName": "default",
  "from": "5542988216891",
  "message": "Olá, preciso reagendar minha consulta",
  "timestamp": "2025-01-11T10:30:00Z"
}
```

**Response**:
```json
{
  "success": true,
  "message": "Mensagem recebida"
}
```

#### ✅ Verificar Status
```
GET /webhook/status
```

**Response**:
```json
{
  "status": "active",
  "timestamp": "2025-01-11T10:30:00Z",
  "webhookUrl": "https://seu-site.com/webhook/whatsapp"
}
```

---

### 4. **server-baileys.js (Bot WhatsApp)**
**Localização**: `whatsapp-bot/`

**Funcionalidades**:
- ✅ Conexão com WhatsApp via Baileys
- ✅ QR Code para autenticação
- ✅ Persistência de sessão no PostgreSQL
- ✅ Recepção de mensagens
- ✅ Envio para webhook ASP.NET

**Tabelas PostgreSQL**:

**WhatsAppSessions**:
```sql
CREATE TABLE "WhatsAppSessions" (
  "Id" SERIAL PRIMARY KEY,
  "SessionName" VARCHAR(100) UNIQUE,
  "Status" VARCHAR(50),
  "PhoneNumber" VARCHAR(50),
  "QRCode" TEXT,
  "QRCodeExpiry" TIMESTAMP,
  "LastConnection" TIMESTAMP,
  "UpdatedAt" TIMESTAMP
);
```

**WhatsAppMessages**:
```sql
CREATE TABLE "WhatsAppMessages" (
  "Id" SERIAL PRIMARY KEY,
  "SessionName" VARCHAR(100),
  "From" VARCHAR(50),
  "Message" TEXT,
  "MessageData" JSONB,
  "ReceivedAt" TIMESTAMP
);
```

---

## ⚙️ Configuração

### appsettings.json

```json
{
  "WhatsApp": {
    "BotUrl": "https://whatsapp-bot-production-0624.up.railway.app",
    "BotAtivo": true,
    "NumeroAtendente": "5542988216891",
    "SiteUrl": "https://seu-site.com",
    "VerifyToken": "clinicapsi_webhook_token_2025"
  },
  "Clinica": {
    "Nome": "Clínica de Psicologia",
    "Endereco": "Rua Example, 123 - Centro - Cidade/UF",
    "Telefone": "(42) 98821-6891"
  }
}
```

### Variáveis de Ambiente (Railway - Bot)

```bash
DATABASE_URL=postgres://user:pass@host:port/db
PORT=3000
NODE_ENV=production
ASPNET_WEBHOOK_URL=https://seu-site.com
```

---

## 🚀 Como Executar

### 1. Iniciar o Bot WhatsApp

```bash
cd whatsapp-bot
npm install
npm start
```

**Conectar WhatsApp**:
1. Acesse: `https://whatsapp-bot-production-0624.up.railway.app/qrcode/default`
2. Escaneie o QR Code com WhatsApp Web
3. Bot conectado! ✅

### 2. Iniciar ASP.NET

```bash
dotnet run --project src/ClinicaPsi.Web
```

**Verificar Background Service**:
- Logs devem mostrar: `🤖 WhatsApp Notification Background Service iniciado`
- Próxima execução agendada automaticamente

---

## 📬 Fluxo de Notificações

### 1️⃣ Notificação Automática (24h antes)

```mermaid
graph LR
    A[Background Service<br/>09:00] --> B[Query Consultas<br/>Amanhã]
    B --> C[Formatar Telefone<br/>+55]
    C --> D[Criar Mensagem<br/>Template]
    D --> E[Enviar via<br/>WhatsApp Bot]
    E --> F[Salvar no<br/>PostgreSQL]
```

**Exemplo de Mensagem**:
```
🏥 *Lembrete de Consulta*

Olá *João Silva*!

📅 Você tem uma consulta agendada para:
• *Segunda-feira, 12/01/2025*
• *Horário:* 14:00
• *Psicóloga:* Dra. Ana Santos

⏰ *Importante:* Chegue com 10 minutos de antecedência.

---

*Precisa reagendar ou cancelar?*

Acesse nosso site: https://seu-site.com
1️⃣ Faça login com seu e-mail
2️⃣ Vá em "Minhas Consultas"
3️⃣ Clique em "Reagendar" ou "Cancelar"

💡 *Dica:* Reagendamentos devem ser feitos com pelo menos 24h de antecedência.

---

Se tiver alguma dúvida, responda esta mensagem que um atendente entrará em contato! 😊
```

---

### 2️⃣ Paciente Responde ao Bot

```mermaid
graph LR
    A[Paciente<br/>Envia Mensagem] --> B[Bot Recebe<br/>Baileys]
    B --> C[Salva no<br/>PostgreSQL]
    C --> D[Webhook POST<br/>ASP.NET]
    D --> E[ProcessarMensagem<br/>Service]
    E --> F{Tipo<br/>Mensagem?}
    F -->|Saudação| G[Menu Opções]
    F -->|Reagendar| H[Instruções Site]
    F -->|Dúvida| I[Encaminhar Atendente]
    G --> J[Bot Responde]
    H --> J
    I --> J
    J --> K[Enviar via<br/>WhatsApp Bot]
```

**Exemplo de Interação**:

**Paciente**: "Olá"

**Bot**:
```
🏥 Olá *João Silva*! Bem-vindo(a) à Psicóloga Ana Santos! 😊

Como posso ajudar você hoje?

📋 *Menu de Opções:*

1️⃣ Reagendar ou Cancelar consulta
2️⃣ Ver horários disponíveis
3️⃣ Endereço e localização
4️⃣ Valores das consultas
5️⃣ Falar com atendente

Digite o número da opção ou me envie sua dúvida diretamente!

💻 *Site:* https://seu-site.com
```

---

## 🔄 Ciclo de Vida do Serviço

### Background Service

```
Startup
   │
   ├─► Calcular Próxima Execução (09:00)
   │
   ├─► Aguardar até 09:00
   │
   ├─► Executar EnviarNotificacoesConsultasAmanha()
   │   ├─► Query Consultas (DataHorario BETWEEN agora+23h AND agora+25h)
   │   ├─► Para cada Consulta:
   │   │   ├─► Formatar Telefone (LimparTelefone)
   │   │   ├─► Criar Mensagem (Template)
   │   │   ├─► Enviar (WhatsAppWebService → Bot → WhatsApp)
   │   │   └─► Aguardar 2 segundos
   │   └─► Log: "✅ Notificações enviadas"
   │
   ├─► Aguardar 24 horas
   │
   └─► Repetir ciclo
```

---

## 🧪 Testes

### Testar Envio de Notificação Manual

1. Criar consulta para amanhã no banco de dados
2. Executar método manualmente:

```csharp
// Em um controller ou página de teste
var notificationService = _serviceProvider.GetRequiredService<WhatsAppNotificationService>();
await notificationService.EnviarNotificacoesConsultasAmanha();
```

3. Verificar se mensagem foi enviada para o WhatsApp do paciente

### Testar Bot Inteligente

Enviar mensagens para o número do bot:

```
Teste 1: "Olá" → Deve receber menu de opções
Teste 2: "Reagendar" → Deve receber instruções + link
Teste 3: "Quanto custa?" → Deve receber info sobre valores
Teste 4: "Tenho uma dúvida complexa" → Deve encaminhar para atendente
```

---

## 📊 Monitoramento

### Logs Importantes

**Background Service**:
```
🤖 WhatsApp Notification Background Service iniciado
⏰ Próximo envio de notificações: 12/01/2025 09:00 (23h 45min)
📨 Iniciando envio de notificações...
✅ Notificações WhatsApp enviadas com sucesso!
```

**Webhook Controller**:
```
📨 Webhook recebido: 5542988216891 - Olá, preciso reagendar
📨 Mensagem recebida de 5542988216891: Olá, preciso reagendar
💾 Mensagem salva no banco
📡 Enviando para ASP.NET: https://seu-site.com/webhook/whatsapp
✅ Mensagem processada pelo ASP.NET
```

**Notification Service**:
```
📨 Mensagem recebida de 5542988216891: Olá, preciso reagendar
✅ Notificação enviada para João Silva - Consulta 12/01/2025 14:00
📨 Mensagem encaminhada para atendente
```

---

## 🔒 Segurança

### Autenticação do Webhook

O webhook ASP.NET valida token configurado em `appsettings.json`:

```csharp
var expectedToken = _configuration["WhatsApp:VerifyToken"];
if (request.Token != expectedToken) {
    return Unauthorized();
}
```

### Validação de Telefone

Telefones são limpos e validados antes do envio:

```csharp
private string LimparTelefone(string telefone)
{
    // Remove tudo exceto números
    var limpo = new string(telefone.Where(char.IsDigit).ToArray());
    
    // Adiciona código do país se necessário
    if (!limpo.StartsWith("55"))
        limpo = "55" + limpo;
    
    return limpo;
}
```

---

## 📖 Referências

### Documentação

- **Baileys**: https://github.com/WhiskeySockets/Baileys
- **ASP.NET Background Services**: https://docs.microsoft.com/aspnet/core/fundamentals/host/hosted-services
- **PostgreSQL**: https://www.postgresql.org/docs/

### Arquivos Relacionados

- `WhatsAppNotificationService.cs` - Serviço principal
- `WhatsAppNotificationBackgroundService.cs` - Background worker
- `WhatsAppWebhookController.cs` - Controller webhook
- `WhatsAppWebService.cs` - Bridge para bot
- `server-baileys.js` - Bot WhatsApp (Node.js)
- `appsettings.json` - Configurações

---

## 🎉 Status Final

### ✅ Implementações Concluídas

- [x] Bot WhatsApp conectado e funcionando
- [x] Notificações automáticas 24h antes
- [x] Formatação de telefone com código do país
- [x] Bot inteligente com respostas automatizadas
- [x] Encaminhamento para atendente humano
- [x] Background service rodando diariamente
- [x] Webhook recebendo mensagens
- [x] Integração completa ASP.NET ↔ Bot

### 🚀 Próximos Passos (Opcional)

- [ ] Dashboard para visualizar notificações enviadas
- [ ] Relatórios de mensagens recebidas
- [ ] Configuração de horário de envio via admin
- [ ] Templates customizáveis de mensagens
- [ ] Integração com múltiplos psicólogos
- [ ] Notificações via SMS (backup)

---

## 📞 Suporte

**Número do Bot**: 554288593775  
**Nome**: Psicóloga Ana Santos  
**Status**: ✅ Conectado

**Webhook ASP.NET**: `https://seu-site.com/webhook/whatsapp`  
**Bot Railway**: `https://whatsapp-bot-production-0624.up.railway.app`

---

**Última Atualização**: 11 de Janeiro de 2025  
**Versão**: 1.0.0
